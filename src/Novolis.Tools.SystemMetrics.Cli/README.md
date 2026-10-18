# Novolis.Tools.SystemMetrics.Cli

`novolis-system-metrics` is a Spectre.Console developer tool over
`Novolis.IO.SystemMetrics`. It shows host inventory and captures a bounded
time-span of CPU, process, memory, GPU, network, and screen-topology data.

## Install

```powershell
dotnet tool install --global Novolis.Tools.SystemMetrics.Cli --version 2026.1.*
```

## Quick start

Use `inventory` for a one-time resource and display snapshot, or `capture`
for a live bounded session.

## Inventory

```powershell
novolis-system-metrics inventory
novolis-system-metrics inventory --json
```

## Capture

Capture the current system:

```powershell
novolis-system-metrics capture --duration 10 --interval 250
```

Capture a game or rendering process by PID:

```powershell
novolis-system-metrics capture --pid 1234 --duration 30 --interval 250
```

Use `--adapter` with the selected adapter id to record the workload/resource
correlation marker. `--json` emits one summary document for scripts and CI.

GPU utilization and memory counters depend on the operating system and driver.
The tool shows the provider status instead of treating unsupported values as
zero.
