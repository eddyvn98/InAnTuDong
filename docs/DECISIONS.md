# Architecture Decision Log

## ADR-001 - Windows-first hybrid desktop
Status: accepted

Use a Windows native agent/host with web-based UI capability. Pure browser code cannot reliably own the required local file/printer/scanner integration.

## ADR-002 - Bypass manufacturer UI
Status: accepted

Use Windows printing APIs and installed drivers for normal jobs. Epson UI automation is fallback only.

## ADR-003 - PrintJobSpec is the execution boundary
Status: accepted

AI produces structured intent. Validated deterministic code owns execution.

## ADR-004 - Millimetres are canonical
Status: accepted

Physical geometry is stored/calculated in millimetres and converted only at rendering/device boundaries.

## ADR-005 - Library-first commodity implementation
Status: accepted

Use mature libraries/platform APIs where they remove meaningful commodity code. Keep custom code focused on intent, validation, layout policy and orchestration.

## ADR-006 - .NET 10 baseline
Status: accepted

Use .NET 10 for the new codebase.

## ADR-007 - Railway hosts the cross-platform web surface only
Status: accepted

Railway verifies/demoes shared domain/layout/web behavior. Windows printer/scanner execution remains on a local Windows agent.
