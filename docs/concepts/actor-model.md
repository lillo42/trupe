# The Actor Model

## Overview

The **Actor Model** is a mathematical model of concurrent computation first introduced by Carl Hewitt in 1973. It treats _actors_ as the fundamental building blocks of concurrent systems. Instead of sharing memory between threads — which leads to race conditions, deadlocks, and hard-to-debug bugs — the actor model enforces communication through **asynchronous message passing**.

Each actor is a self-contained unit of computation that:

1. **Has its own private state** — no other actor can directly read or modify it.
2. **Processes messages sequentially** — one message at a time, eliminating internal concurrency issues.
3. **Communicates only via messages** — actors never call methods on each other directly.
4. **Can create other actors** — forming hierarchies and supervision trees.

## Why Use the Actor Model?

### The Problem with Shared State

Traditional multithreaded programming relies on shared memory protected by locks, mutexes, or semaphores. This approach is error-prone:

```
Thread A                    Thread B
────────                    ────────
lock(resource)
  read value                lock(resource)   ← blocks
  modify value                ...
  write value                 ...
unlock(resource)
                            read value
                            modify value
                            write value
                            unlock(resource)
```

Even simple operations require careful synchronization. Forgetting a lock, holding locks in the wrong order, or mixing lock granularities leads to bugs that are hard to reproduce and fix.

### The Actor Model Solution

Actors eliminate shared state entirely. Each actor owns its data, and all interactions happen through message passing:

```
Actor A                     Actor B
───────                     ───────
receives message            
  updates own state          receives message
  sends message to B →        updates own state
                               sends response to A →
receives response
  updates own state
```

There are no locks, no race conditions, and no deadlocks. Each actor processes one message at a time, making the behavior deterministic and easy to reason about.

## Core Principles

### 1. Isolation

An actor's state is completely private. No other actor or external code can access it directly. The only way to interact with an actor is to send it a message.

### 2. Message Passing

Communication between actors is asynchronous. When Actor A sends a message to Actor B, it does not wait for B to process it (fire-and-forget). If a response is needed, it can use a request-response pattern (ask).

### 3. Sequential Processing

Each actor has a **mailbox** (a message queue). Messages are delivered to the mailbox and processed one at a time, in order. This guarantees that the actor's internal state is never accessed concurrently.

### 4. Supervision

When an actor fails, its **supervisor** (the actor that created it) decides what to do: restart it, stop it, escalate the failure, or resume execution. This is known as "let it crash" philosophy — instead of writing defensive code everywhere, you build a supervision hierarchy that handles failures automatically.

### 5. Location Transparency

Actors are referenced through an `IActorReference`, not by direct object references. This means actor code does not need to know whether the target actor lives in the same process, on another thread, or potentially on another machine.

## How Trupe Implements the Actor Model

Trupe brings the actor model to .NET with a focus on simplicity and performance:

| Concept | Trupe Implementation |
|---------|---------------------|
| Actor | `Actor` base class or `IActor` interface |
| Message | Any .NET object (records recommended) |
| Mailbox | `ChannelMailbox` backed by `System.Threading.Channels` |
| Actor Reference | `IActorReference` with `Tell` and `Ask` methods |
| Supervision | `Supervisor` base class with configurable strategies |
| Actor Creation | `IActorFactory` with dependency injection support |
| Actor Registry | `IActorRegister` for looking up actors by name |

The next sections will walk you through creating actors, handling messages, and building supervision trees.
