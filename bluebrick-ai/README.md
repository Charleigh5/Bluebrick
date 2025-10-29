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
2. Create a Python virtual environment and install dependencies:

   ```bash
   python -m venv .venv
   .venv\\Scripts\\activate
   pip install -e .
   ```

3. Copy `configs/env.example` to `.env` and update the values with your local
   SolidWorks, EPDM, and LLM configuration.
4. Ensure the SolidWorks and EPDM COM libraries are registered (typically part
   of the standard installation). The bootstrap scripts under `scripts/` can be
   extended to enforce prerequisites.

### Running the Sample Workflow

The sample script under `samples/create_and_checkin_plate.py` demonstrates an
end-to-end operation that creates a simple plate, saves it, and checks it into
EPDM.

```bash
python -m samples.create_and_checkin_plate
```

> **Note:** SolidWorks COM operations must run within a single-threaded
> apartment (STA). When integrating with async frameworks or task queues, use a
> dedicated worker thread that initializes COM (see `SolidWorksApp`).

## Core Components

- **CAD Adapters (`src/cad/`)**: Provide thin wrappers over SolidWorks and EPDM
  COM interfaces.
- **Agent Orchestration (`src/agents/`)**: LangChain-based tooling that bridges
  LLM instructions with deterministic CAD operations.
- **Workflows (`src/workflows/`)**: Higher-level processes that chain together
  multiple CAD steps, state tracking, and PDM actions.
- **Utilities (`src/utils/`)**: Support modules such as context management and
  serialization.

## Next Steps

- Implement robust error handling and logging via `logging.yaml` configuration.
- Add integration tests using mock COM interfaces to validate agent tooling.
- Extend the tool library with geometry primitives, drawing creation, and BOM
  management.
- Integrate telemetry and audit trails for production deployments.

## License

Proprietary — internal use within the BlueBrick engineering organization.
