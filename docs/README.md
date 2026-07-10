# Trupe

A lightweight, high-performance .NET implementation of the Actor Model for learning and practical use.

## What is Trupe?

Trupe is an actor model framework for .NET that provides a simple yet powerful way to build concurrent and distributed applications. Actors are isolated units of computation that communicate exclusively through asynchronous message passing, eliminating shared state and the complexity of traditional multi-threaded programming.

## Key Features

- **Simple API** — Easy-to-use abstractions for creating actors and handling messages.
- **Type-Safe Message Handling** — Strongly-typed message handlers with compile-time checking.
- **Supervision Strategies** — Built-in support for One-for-One and All-for-One supervision.
- **AOT Compatible** — Full support for Native Ahead-of-Time compilation.
- **High Performance** — Channel-based mailboxes for efficient message delivery.
- **Lifecycle Management** — Initialize, restart, and cleanup hooks for actors.

## Packages

| Package | Description |
|---------|-------------|
| `Trupe.Abstractions` | Core interfaces and abstractions (`IActor`, `ISupervisor`, `IActorReference`, etc.) |
| `Trupe` | Actor model implementation with supervision, mailboxes, and DI integration |
| `Trupe.Extensions.Hosting` | `IHostedService` integration for managing actor system lifecycle |
| `Trupe.OpenTelemetry` | Tracing and metrics instrumentation (`AddTrupeInstrumentation`) |

## Requirements

- .NET 8.0, 9.0, or 10.0
- .NET Framework 4.6.2
- .NET Standard 2.0
