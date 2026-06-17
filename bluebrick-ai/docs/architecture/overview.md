# Bluebrick AI Architecture Overview

This document summarizes the layered architecture used by the Bluebrick AI
platform. It mirrors the material in the top-level README so the design intent
is easily accessible from within the documentation set.

## Layers

1. **Agent Orchestration** – LangChain-based agents expose SolidWorks and PDM
   tools that can be composed into goal-driven workflows.
2. **CAD Connectors** – COM wrappers for SolidWorks and EPDM provide atomic
   operations such as creating parts and checking files in or out of the vault.
3. **Workflow Engine** – Reusable workflows (e.g., the simple plate example)
   coordinate multi-step tasks that bridge CAD edits and PDM lifecycle events.
4. **Shared Utilities** – Utilities like the design context track metadata,
   design history, and audit trails for downstream analytics.

## Execution Model

All SolidWorks and EPDM calls run inside a single-threaded apartment (STA)
worker to meet COM threading requirements. Higher-level layers communicate with
this worker via the orchestrator APIs, ensuring thread-safe access to CAD
resources while enabling asynchronous job management.
