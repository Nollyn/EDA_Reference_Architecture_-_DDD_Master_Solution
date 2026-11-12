# Order Service — EDA Reference Architecture & DDD Master Solution

[![.NET](https://img.shields.io/badge/.NET-8.0%20LTS-purple.svg)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-13.0-blue.svg)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![Architecture](https://img.shields.io/badge/Architecture-Hexagonal%20%2F%20Ports%20%26%20Adapters-green.svg)]()
[![Pattern](https://img.shields.io/badge/Patterns-DDD%20%7C%20Outbox%20%7C%20Inbox%20%7C%20Saga-orange.svg)]()
[![Observability](https://img.shields.io/badge/Observability-OpenTelemetry%20%7C%20Tempo%20%7C%20Prometheus-red.svg)]()

> Solución de referencia de nivel **Principal Software Engineer** en .NET moderna, diseñada bajo los estándares más exigentes de la industria: **Arquitectura Hexagonal (Ports & Adapters)** estricta, **Domain-Driven Design (DDD)** táctico, **Event-Driven Architecture (EDA)**, **Transactional Outbox/Inbox**, resiliencia con **Polly v8** y **Observabilidad de extremo a extremo con OpenTelemetry**.

---

## 1. Arquitectura y Estructura de la Solución

La solución implementa una separación física estricta de proyectos para garantizar la regla de dependencias por compilación:

```
EDA-Reference-Portfolio/
├── src/
│   ├── OrderService.Domain/          # Núcleo puro de negocio (Cero dependencias externas)
│   │   ├── Aggregates/               # Order (Aggregate Root)
│   │   ├── Entities/                 # OrderItem
│   │   ├── ValueObjects/             # Money, Address, OrderStatus, Identificadores fuertemente tipados
│   │   ├── Events/                   # Domain Events (IDomainEvent)
│   │   ├── Exceptions/               # Domain Exceptions
│   │   └── Repositories/             # Interfaces de Repositorio (IOrderRepository)
│   │
│   ├── OrderService.Application/     # Casos de uso y Puertos de aplicación
│   │   ├── Commands/                 # CreateOrder, ConfirmOrder, CancelOrder, Saga Handlers
│   │   ├── Queries/                  # GetOrderById, GetOrdersKeyset (CQRS Read side)
│   │   ├── Common/                   # Mediador desacoplado propio (IMediator / IRequestHandler)
│   │   ├── Ports/                    # IUnitOfWork, IIntegrationEventPublisher, IFraudCheckService, IOrderQueries
│   │   └── Events/                   # Integration Events
│   │
│   ├── OrderService.Infrastructure/  # Adaptadores de Entrada/Salida e Infraestructura técnica
│   │   ├── Persistence/              # EF Core (Escritura/Dominio) y Dapper (Consultas Keyset)
│   │   ├── Messaging/Outbox/         # Transactional Outbox con `FOR UPDATE SKIP LOCKED`
│   │   ├── Messaging/Inbox/          # Consumidor Idempotente con Dead Letter Queue (DLQ)
│   │   ├── Messaging/RabbitMq/       # Cliente nativo RabbitMQ.Client v7+ asíncrono
│   │   ├── ExternalServices/         # Cliente HTTP antifraude resiliente con Polly v8
│   │   └── Observability/            # OpenTelemetry (Traces, Metrics, W3C TraceContext)
│   │
│   ├── OrderService.Api/             # Punto de entrada HTTP (Minimal APIs)
│   │   ├── Endpoints/                # /api/v1/orders
│   │   └── Middleware/               # ProblemDetails (RFC 9457), CorrelationIdMiddleware, HealthChecks
│   │
│   └── OrderService.Worker/          # BackgroundService independiente (Outbox Processor & Inbox Consumer)
│
├── tests/
│   ├── OrderService.Domain.Tests/    # Tests unitarios puros de invariantes de dominio (xUnit, FluentAssertions, NSubstitute)
│   └── OrderService.Integration.Tests/# Tests E2E con Testcontainers (PostgreSQL + RabbitMQ reales)
│
├── docs/
│   └── sql-scaling.md                # Diseño de índices compuestos, particionado y Keyset pagination
│
└── docker-compose.yml                # Stack completo de infraestructura local
```

---

## 2. Puntos Clave de Diseño y Patrones Técnicos

### A. Dominio y CQRS Ligero
- **Aggregate Root `Order`:** Encapsula todas las invariantes de negocio (un pedido no puede confirmarse sin líneas, no se puede modificar si ya está pagado o enviado, total consistente).
- **Concurrencia Optimista:** Mapeo de token de concurrencia `version` en EF Core, capturando `DbUpdateConcurrencyException` y traduciéndola a `409 Conflict` (ProblemDetails RFC 9457).
- **Keyset Pagination (Dapper):** Consultas `GET /api/v1/orders` en $\mathcal{O}(1)$ sobre el índice compuesto `(created_at DESC, id DESC)` evitando el costo lineal de `OFFSET`.

### B. Mensajería Fiable y Resiliente (RabbitMQ.Client v7+ Nativo)
- **Abstracción Pragmática:** Se abstrae únicamente la intención de publicación (`IIntegrationEventPublisher`) y consumo (`IIntegrationEventHandler<T>`), sin generar *leaky abstractions* sobre topologías internas.
- **Transactional Outbox:** Guardado atómico de la orden y del evento de integración en la misma transacción ACID de PostgreSQL.
- **Worker con `FOR UPDATE SKIP LOCKED`:** Permite que múltiples réplicas del Worker consuman el Outbox en paralelo sin contención ni duplicados, publicando con *Publisher Confirms*.
- **Consumidor Idempotente (Inbox):** Desactiva `autoAck`, valida en la tabla `processed_messages` (`message_id` PK) dentro de la transacción de negocio y realiza `BasicAck` explícito.
- **Reintentos y DLQ:** Topología con Main Exchange (`orders.topic`), Retry Exchange con TTL (`orders.retry.direct`) y Dead Letter Exchange/Queue (`orders.dlx` / `orders.v1.dlq`) tras 3 intentos fallidos.

### C. Saga Coreografiada con Compensación
1. `CreateOrderCommand` -> Persiste `Order` y publica `OrderCreatedIntegrationEvent`.
2. Si el servicio de inventario emite `InventoryReservationFailedIntegrationEvent`, el Worker ejecuta la acción compensatoria (`ProcessInventoryReservationFailedCommand`), cancela la orden con motivo explícito y emite `OrderCancelledIntegrationEvent`.

### D. Observabilidad de Extremo a Extremo
- **Propagación Distribuida W3C:** Inyección de `traceparent` y `tracestate` en los headers de RabbitMQ al publicar desde el Outbox y extracción en el Worker como `ActivityKind.Consumer`, logrando una traza unificada desde la petición HTTP hasta el procesamiento asíncrono.
- **Métricas:** Contadores y medidores personalizados (`orders.created.count`, `outbox.pending.messages`, `rabbitmq.dlq.messages`).

---

## 3. Puesta en Marcha del Entorno Local

### Requisitos
- [.NET 8 SDK](https://dotnet.microsoft.com/)
- [Docker & Docker Compose](https://www.docker.com/)

### Levantar Todo el Stack con Docker Compose
Ejecuta el siguiente comando en la raíz del proyecto:

```bash
docker compose up -d --build
```

### URLs de Acceso a Servicios
| Servicio | URL | Credenciales |
| :--- | :--- | :--- |
| **Order Service API (Swagger)** | [http://localhost:8080/swagger](http://localhost:8080/swagger) | - |
| **RabbitMQ Management UI** | [http://localhost:15672](http://localhost:15672) | `guest` / `guest` |
| **Grafana (Dashboards & Traces)**| [http://localhost:3000](http://localhost:3000) | `admin` / `admin` |
| **Prometheus (Métricas)** | [http://localhost:9090](http://localhost:9090) | - |
| **Tempo (Traces)** | [http://localhost:3200](http://localhost:3200) | - |
| **Health Checks (Liveness)** | [http://localhost:8080/health/live](http://localhost:8080/health/live) | - |
| **Health Checks (Readiness)** | [http://localhost:8080/health/ready](http://localhost:8080/health/ready) | - |

---

## 4. Ejecución de Pruebas

### Pruebas Unitarias de Dominio
Ejecuta los tests unitarios puros de invariantes de negocio:

```bash
dotnet test tests/OrderService.Domain.Tests/OrderService.Domain.Tests.csproj
```

### Pruebas de Integración (Testcontainers)
Ejecuta los tests end-to-end que levantan contenedores reales de PostgreSQL y RabbitMQ:

```bash
dotnet test tests/OrderService.Integration.Tests/OrderService.Integration.Tests.csproj
```

### Compilar Toda la Solución
```bash
dotnet build
```

---

## 5. Decisiones de Arquitectura (Architecture Decision Records - ADRs)

---

### ADR-001: Abstracción del Broker de Mensajería y Traducción a Azure Service Bus

#### Contexto
En sistemas orientados a eventos, es común caer en dos extremos perjudiciales:
1. **Acoplamiento Fuerte:** Esparcir tipos específicos del broker (`RabbitMQ.Client`, `Azure.Messaging.ServiceBus`) en la capa de aplicación.
2. **Abstracción Excesiva (*Leaky / Bloated Abstractions*):** Utilizar frameworks pesados que intentan unificar topologías dispares (exchanges, topics, queues, routing keys, subscriptions) ocultando las capacidades nativas del broker subyacente.

#### Decisión
Se decidió implementar una **abstracción pragmática mínima** en la capa de aplicación compuesta exclusivamente por dos puertos:
- `IIntegrationEventPublisher`: `PublishAsync<T>(T event)`
- `IIntegrationEventHandler<T>`: `HandleAsync(T event)`

Toda la topología específica (Exchanges, Bindings, Retry TTL y DLX en RabbitMQ) reside exclusivamente en `OrderService.Infrastructure`.

#### Traducción del Diseño a Azure Service Bus
Si la solución se despliega en **Microsoft Azure**, la infraestructura se traduce de forma natural sin tocar una sola línea de la capa de Dominio o Aplicación:

| Concepto en RabbitMQ (Implementado) | Equivalente Nativo en Azure Service Bus |
| :--- | :--- |
| **Topic Exchange (`orders.topic`)** | **Service Bus Topic** (`sb-orders-topic`) |
| **Queues / Bindings (`OrderService.#`)** | **Topic Subscriptions** con *SqlRuleFilter* (`user.eventType = 'OrderCreated'`) |
| **Ordenamiento FIFO y Concurrencia** | **Service Bus Message Sessions** (`SessionId = order.Id.ToString()`), garantizando procesamiento secuencial estricto por agregado sin bloquear otros pedidos. |
| **Reintentos y Backoff** | Configuración nativa de **`MaxDeliveryCount`** (ej. 3 o 5) en la suscripción. |
| **Dead Letter Queue (DLQ)** | **`$DeadLetterQueue` nativa** de Azure Service Bus (`sb-orders-topic/Subscriptions/sub-orders/$DeadLetterQueue`), con motivo automático en `DeadLetterReason`. |
| **Idempotencia / Deduplicación** | **`RequiresDuplicateDetection = true`** en el Topic usando `MessageId = event.Id` dentro de una ventana de deduplicación de 10 minutos, complementando la tabla `ProcessedMessages`. |

---

### ADR-002: Estrategia de Migración desde un Monolito (Strangler Fig + ACL)

#### Contexto
La migración de un módulo crítico de pedidos desde un monolito legado hacia este microservicio EDA debe realizarse sin interrumpir la operación del negocio (*zero downtime*), evitando el antipatrón de reescritura total (*Big Bang rewrite*).

#### Estrategia de Migración Implementada:

```
[ Clientes / Web / Mobile ]
           │
           ▼
┌─────────────────────────────────────────┐
│ API Gateway / Reverse Proxy (YARP / APIM)│
└────┬───────────────────────────────┬────┘
     │ (Rutas no migradas)           │ (POST /api/v1/orders)
     ▼                               ▼
┌─────────────────────────┐     ┌───────────────────────────────────┐
│ Monolito Legado         │     │ OrderService (Nuevo Microservicio)│
│                         │     └─────────────────┬─────────────────┘
│ ┌─────────────────────┐ │                       │
│ │ Anti-Corruption     │ │◄──────────────────────┘
│ │ Layer (ACL) Adapter │ │  Eventos: OrderCreated, OrderCancelled
│ └─────────────────────┘ │  (Sincronización Bidireccional Asíncrona)
└─────────────────────────┘
```

1. **Paso 1: Intercepción en API Gateway (Strangler Fig Pattern):**
   - Se configura un proxy inverso (ej. YARP o Azure API Management) frente al monolito.
   - Las rutas nuevas `POST /api/v1/orders` y `GET /api/v1/orders` se desvían de forma transparente al nuevo `OrderService`, mientras que facturación o logística continúan en el monolito.

2. **Paso 2: Anti-Corruption Layer (ACL) Bidireccional:**
   - Para evitar que los modelos de datos relacionales arcaicos del monolito contaminen el diseño DDD de `OrderService`, se implementa una capa ACL.
   - Cuando `OrderService` publica `OrderCreatedIntegrationEvent`, un consumidor en el ACL traduce el evento al formato de tablas legadas (`tbl_pedidos_legacy`) para mantener sincronizados los módulos que aún residen en el monolito.

3. **Paso 3: Doble Escritura Verificada / Dark Launching:**
   - Durante las primeras semanas, se ejecutan verificaciones asíncronas de reconciliación de datos entre ambos esquemas para validar la consistencia antes del apagado definitivo de las tablas de pedidos en el monolito.

4. **Paso 4: Estrangulamiento Completo y Decomiso:**
   - Una vez que todos los consumidores downstream migran a eventos de RabbitMQ, el código de pedidos del monolito se elimina de forma segura.

---

### ADR-003: Definición de SLIs/SLOs e Impacto Directo en Métricas DORA

#### Service Level Indicators (SLIs) y Service Level Objectives (SLOs)

| Tipo de Métrica | Service Level Indicator (SLI) | Service Level Objective (SLO) | Ventana de Medición |
| :--- | :--- | :--- | :--- |
| **Disponibilidad API** | Ratio de peticiones HTTP `POST /api/v1/orders` exitosas (`2xx`) sobre el total de peticiones no-cliente (`4xx` excluidos). | **$\ge 99.9\%$** | Ventana móvil de 30 días |
| **Latencia de Escritura** | Tiempo de respuesta del endpoint `POST /api/v1/orders` medido en el API Gateway. | **p95 < 150ms**<br>**p99 < 300ms** | Ventana móvil de 7 días |
| **Lag de Publicación Outbox** | Tiempo transcurrido entre `outbox_messages.occurred_on` y la publicación efectiva en RabbitMQ. | **p99 < 1.0s** | Ventana móvil de 24 horas |
| **Throughput de Compensación Saga** | Tiempo desde la llegada de `InventoryReservationFailed` hasta el estado `Cancelled` en DB. | **p99 < 2.0s** | Ventana móvil de 24 horas |

---

#### Impacto Directo en las 4 Métricas DORA

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                             DORA METRICS IMPACT                             │
├───────────────────────────────┬─────────────────────────────────────────────┤
│ Deployment Frequency (DF)     │ Elite: Múltiples despliegues diarios        │
│ Lead Time for Changes (LTTC)  │ Elite: < 1 hora (commit a producción)       │
│ Change Failure Rate (CFR)     │ Elite: < 2% (Invariantes DDD + Polly v8)   │
│ Mean Time to Recovery (MTTR)  │ Elite: < 5 minutos (OTel Traces + DLQ)      │
└───────────────────────────────┴─────────────────────────────────────────────┘
```

1. **Deployment Frequency (DF) & Lead Time for Changes (LTTC):**
   - La **Arquitectura Hexagonal** desacopla completamente el núcleo de negocio de la base de datos y del broker de mensajería. Los cambios de reglas de dominio se prueban en milisegundos mediante tests unitarios puros, eliminando pipelines lentos y permitiendo despliegues continuos independientes.
   - Las migraciones aditivas de base de datos y la compatibilidad hacia atrás en los contratos de eventos (`IntegrationEvent`) garantizan despliegues *Zero-Downtime* (Blue/Green o Canary).

2. **Change Failure Rate (CFR):**
   - El **DDD Táctico** hace que los estados inválidos sean irrepresentables en código (invariantes estrictas en `Order.cs`).
   - Las políticas de resiliencia con **Polly v8** (Retry con backoff exponencial y jitter, Circuit Breaker) mitigan fallos transitorios en llamadas a servicios externos, reduciendo drásticamente la tasa de fallos en producción.

3. **Mean Time to Recovery (MTTR) y Gestión del Error Budget:**
   - Gracias a la **propagación W3C de OpenTelemetry** y la instrumentación continua hacia Grafana Tempo, los ingenieros de guardia pueden correlacionar una alerta de error en segundos inspeccionando la traza distribuida exacta entre la API, el Outbox y el Worker.
   - El patrón **Inbox + DLQ** asegura que ningún mensaje se pierda: ante caídas de dependencias downstream, los mensajes se retienen en la cola de reintentos o DLQ y pueden reinyectarse automáticamente una vez restablecido el servicio, reduciendo el MTTR de horas a minutos sin pérdida de datos.
   - **Gobernanza por Error Budget:** Si el SLO de disponibilidad ($99.9\%$) consume más del $20\%$ de su Error Budget en una semana, las alertas de Prometheus congelan automáticamente los despliegues de nuevas funcionalidades no críticas para priorizar la estabilidad operativa.
