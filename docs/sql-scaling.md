# Estrategia de Escalabilidad SQL, Indexación y Particionado

Este documento detalla el diseño de persistencia de alta concurrencia y consultas a gran escala implementado en **OrderService**, optimizado para maximizar el throughput transaccional, reducir la latencia de lectura y evitar la degradación de rendimiento por volumen de datos.

---

## 1. Paginación por Keyset (Cursor-Based) vs. Offset

### El Problema de `OFFSET / LIMIT` en Sistemas a Escala
El enfoque tradicional `LIMIT N OFFSET M` obliga al motor relacional (PostgreSQL) a escanear, ordenar y descartar $M$ filas antes de retornar las $N$ solicitadas. A medida que $M$ crece ($M > 100,000$), el costo en I/O y CPU crece de forma lineal $\mathcal{O}(M + N)$, provocando contención de buffer pool y latencias inaceptables en endpoints de consulta.

### Solución Implementada: Keyset Pagination $\mathcal{O}(1)$
En lugar de desplazar filas, se utiliza un **cursor compuesto determinista** basado en `(created_at, id)`:

```sql
SELECT
    o.id AS Id,
    o.customer_id AS CustomerId,
    o.status AS Status,
    o.total_amount AS TotalAmount,
    o.total_currency AS Currency,
    COUNT(i.id) AS TotalItems,
    o.created_at AS CreatedAt
FROM orders o
LEFT JOIN order_items i ON o.id = i.order_id
WHERE (@CursorCreatedAt IS NULL 
   OR (o.created_at < @CursorCreatedAt) 
   OR (o.created_at = @CursorCreatedAt AND o.id < @CursorId))
GROUP BY o.id, o.customer_id, o.status, o.total_amount, o.total_currency, o.created_at
ORDER BY o.created_at DESC, o.id DESC
LIMIT @Limit;
```

### Índice Compuesto Requerido
Para garantizar que el optimizador de PostgreSQL ejecute un **Index Scan Descendente** sin ordenar en memoria (*Sort Operation Elimination*), se define el siguiente índice:

```sql
CREATE INDEX ix_orders_created_at_id 
ON orders (created_at DESC, id DESC);
```

#### Ventajas:
1. **Tiempo de respuesta constante ($\mathcal{O}(1)$):** La latencia de la página 1 es idéntica a la página 1,000,000.
2. **Inmunidad a anomalías de paginación:** No se omiten ni duplican registros cuando se insertan nuevas órdenes concurrentemente.
3. **Cero operaciones de `Tempdb / Memory Sort`:** El árbol B-Tree provee el ordenamiento físico exacto.

---

## 2. Estrategia de Particionado por Rango (Range Partitioning)

Tanto `orders` como `outbox_messages` son tablas de rápido crecimiento (alto volumen de append e ingestión).

### A. Particionado de la Tabla `orders`
Se particiona por rango mensual sobre la columna `created_at`:

```sql
-- Tabla Maestra Particionada
CREATE TABLE orders (
    id UUID NOT NULL,
    customer_id UUID NOT NULL,
    status VARCHAR(50) NOT NULL,
    total_amount NUMERIC(18,2) NOT NULL,
    total_currency VARCHAR(3) NOT NULL,
    shipping_street VARCHAR(200) NOT NULL,
    shipping_city VARCHAR(100) NOT NULL,
    shipping_state VARCHAR(100) NOT NULL,
    shipping_postal_code VARCHAR(20) NOT NULL,
    shipping_country VARCHAR(100) NOT NULL,
    currency VARCHAR(3) NOT NULL,
    cancellation_reason VARCHAR(500) NULL,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NULL,
    version INT8 NOT NULL DEFAULT 0,
    CONSTRAINT pk_orders PRIMARY KEY (id, created_at)
) PARTITION BY RANGE (created_at);

-- Particiones Mensuales
CREATE TABLE orders_y2026m11 PARTITION OF orders
    FOR VALUES FROM ('2026-11-01 00:00:00+00') TO ('2026-12-01 00:00:00+00');

CREATE TABLE orders_y2026m12 PARTITION OF orders
    FOR VALUES FROM ('2026-12-01 00:00:00+00') TO ('2027-01-01 00:00:00+00');

-- Índice local en cada partición
CREATE INDEX ix_orders_y2026m11_created_at_id 
ON orders_y2026m11 (created_at DESC, id DESC);
```

#### Beneficios:
- **Partition Pruning:** Consultas acotadas por fecha acceden únicamente a los índices de las particiones relevantes.
- **Mantenimiento y Archiving Eficiente:** Las órdenes archivadas de más de $N$ años pueden desacoplarse (`DETACH PARTITION`) y transferirse a almacenamiento frío (Cold Storage / Parquet en S3 o Azure Data Lake) sin ejecutar costosos `DELETE` masivos que generen bloat.

---

### B. Particionado y Purga de la Tabla `outbox_messages`
La tabla `outbox_messages` tiene un ciclo de vida transaccional rápido: los registros se insertan, se publican y se marcan como procesados. Mantener millones de registros procesados en una tabla única degrada el rendimiento de los índices B-Tree y genera sobrecarga en el motor de `VACUUM`.

```sql
-- Particionado por Rango Semanal/Mensual
CREATE TABLE outbox_messages (
    id UUID NOT NULL,
    type VARCHAR(200) NOT NULL,
    content TEXT NOT NULL,
    occurred_on TIMESTAMPTZ NOT NULL,
    processed_on TIMESTAMPTZ NULL,
    error TEXT NULL,
    retry_count INT NOT NULL DEFAULT 0,
    trace_parent VARCHAR(200) NULL,
    trace_state VARCHAR(500) NULL,
    CONSTRAINT pk_outbox_messages PRIMARY KEY (id, occurred_on)
) PARTITION BY RANGE (occurred_on);

-- Índice Parcial Altamente Selectivo para Mensajes Pendientes
CREATE INDEX ix_outbox_messages_pending 
ON outbox_messages (occurred_on ASC) 
WHERE processed_on IS NULL;
```

#### Estrategia de Purga (Retention Policy):
1. **Drop Partition:** Las particiones de más de 14 días con todos los mensajes procesados se eliminan de forma atómica con `DROP TABLE outbox_messages_y2026w44;`, liberando espacio en disco instantáneamente a costo $\mathcal{O}(1)$ sin generar *table bloat*.
2. **Índice Parcial (`WHERE processed_on IS NULL`):** Mantiene el tamaño del índice en memoria extremadamente compacto (solo contiene mensajes en vuelo), garantizando que el `SELECT ... FOR UPDATE SKIP LOCKED` se ejecute en sub-milisegundos.

---

## 3. Concurrencia en Background Workers: `FOR UPDATE SKIP LOCKED`

Para permitir que múltiples instancias concurrentes del Worker lean y procesen el Outbox simultáneamente sin colisiones ni deadlocks:

```sql
SELECT * 
FROM outbox_messages
WHERE processed_on IS NULL
ORDER BY occurred_on ASC
LIMIT 50
FOR UPDATE SKIP LOCKED;
```

### Mecanismo de Bloqueo:
- `FOR UPDATE`: Adquiere un bloqueo exclusivo a nivel de fila (`X-Lock`) sobre los registros seleccionados.
- `SKIP LOCKED`: Si otra réplica del Worker ya tiene bloqueada una fila, PostgreSQL no espera (evitando esperas y bloqueos en cascada); simplemente la omite y selecciona el siguiente lote disponible.
- **Resultado:** Escalabilidad horizontal lineal del proceso publicador de eventos con consistencia transaccional estricta.
