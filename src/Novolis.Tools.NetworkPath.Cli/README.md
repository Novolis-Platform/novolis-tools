# Novolis.Tools.NetworkPath.Cli

`novolis-network-path` inventories local adapters, resolves the selected
egress path, and compares explicit adapter-bound TCP connection probes.

It does not change system routes, claim that a tunnel is definitely a VPN,
or contact an IP-reputation service. The default output hides full local
addresses; use `--show-addresses` for an intentional local diagnostic.

## Install

```powershell
dotnet tool install --global Novolis.Tools.NetworkPath.Cli --version 2026.1.*
```

## Examples

```powershell
novolis-network-path inspect --target example.com:443
novolis-network-path inspect --target example.com:443 --all-adapters --count 5
novolis-network-path inspect --target example.com:443 --format json
```

`--target` is required because each probe creates outbound TCP connection
attempts. `--format json` is stable for automation. Interactive Spectre output
is used only with `--interactive` in a real terminal; redirected execution
never prompts.
