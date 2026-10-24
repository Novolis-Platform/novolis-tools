# Novolis.Tools.AgentDev

Host profile for inspect, debug, build, test, and approved edits over the Novolis agent protocol.

The host speaks `Novolis.Agent` capabilities. It does not own a transport. `novolis-agent-dev` attaches MCP or JSONL stdio. Mutations that write files return a preview first. `change.apply` runs only when `approvalToken` equals the proposal correlation id. Build and test processes start with redirected I/O and no console window.
