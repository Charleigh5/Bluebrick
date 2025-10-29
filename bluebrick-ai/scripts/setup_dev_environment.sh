#!/usr/bin/env bash
set -euo pipefail

PYTHON_VERSION="${PYTHON_VERSION:-3.11}"
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")"/.. && pwd)"

echo "==> Bootstrapping Python environment (version: ${PYTHON_VERSION})"

if ! command -v python &>/dev/null; then
  echo "Python executable not found. Please install Python ${PYTHON_VERSION}." >&2
  exit 1
fi

cd "$PROJECT_ROOT"
python -m venv .venv
source .venv/bin/activate
python -m pip install --upgrade pip
pip install -e .[dev]

echo "==> Environment setup complete"
