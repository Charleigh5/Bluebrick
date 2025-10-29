# BlueBrick AI Platform

An AI-assisted automation framework that orchestrates SolidWorks and Enterprise
PDM (EPDM) operations using a tool-enabled language model agent. This scaffold
provides the foundational structure for building autonomous CAD workflows.

## Repository Layout

```
bluebrick-ai/
├─ docs/                 # Design and API documentation
├─ src/                  # Source code for agents, CAD adapters, workflows
├─ tests/                # Unit and integration test harnesses
├─ configs/              # Environment and logging configuration templates
├─ scripts/              # Bootstrap and operational scripts
├─ samples/              # Example end-to-end flows
└─ pyproject.toml        # Python project metadata and dependencies
```

## Getting Started

### Prerequisites

- Windows workstation with SolidWorks 2022 or newer installed
- EPDM client configured with access to the engineering vault
- Python 3.10+
- Valid LLM provider credentials (e.g., OpenAI API key)

### Environment Setup

1. Clone the repository and navigate to the `bluebrick-ai` directory.
2. Run `scripts/bootstrap.ps1` (Windows) or `scripts/setup_dev_environment.sh`
   (macOS/Linux for mock development) to create a virtual environment and
   install dependencies. Both scripts install editable packages with the
   optional `dev` extras for running tests.
3. Copy `configs/env.example` to `.env` and update the values with your local
   SolidWorks, EPDM, and LLM configuration.
4. Execute `scripts/validate_prerequisites.py` on a SolidWorks workstation to
   confirm required COM registrations. If SolidWorks/EPDM components were
   installed outside the default paths, use `scripts/register_com.ps1` to
   register the relevant libraries.

### Running the Sample Workflow

The sample script under `samples/create_and_checkin_plate.py` demonstrates an
end-to-end operation that creates a simple plate, saves it, and checks it into
EPDM.

```bash
python -m samples.create_and_checkin_plate
```

> **Note:** SolidWorks COM operations must run within a single-threaded
> apartment (STA). When integrating with async frameworks or task queues, use a
> dedicated worker thread that initializes COM. The `StaWorker` utility under
> `src/workers/sta_worker.py` coordinates COM access for the LangChain agent.

### Running Tests

```bash
pytest
```

The provided test suite relies on mocked COM interfaces so it can run on any
platform and within CI. Windows-specific integration tests should be added in a
separate job once hardware-in-the-loop validation is available.

## Core Components

- **CAD Adapters (`src/cad/`)**: Provide resilient SolidWorks and EPDM COM
  wrappers with retry logic, structured logging, and expanded geometry/drawing
  operations.
- **Agent Orchestration (`src/agents/`)**: LangChain-based tooling that bridges
  LLM instructions with deterministic CAD operations executed on the STA worker.
- **Workers (`src/workers/`)**: Dedicated STA dispatcher ensuring all COM calls
  run on a single thread with progress callbacks.
- **Workflows (`src/workflows/`)**: Higher-level processes chaining CAD edits
  with PDM lifecycle actions while recording context state.
- **Utilities (`src/utils/`)**: Support modules such as context management and
  serialization.

## Next Steps

- Configure telemetry, alerting, and operational dashboards for production
  deployments.
- Expand workflow library to cover drawing/BOM publication, approval routing,
  and automated material assignments.
- Harden the agent prompt/response pipeline with validation and safety checks
  before executing destructive operations.

## License

Proprietary — internal use within the BlueBrick engineering organization.
