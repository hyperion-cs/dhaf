# Dhaf
Cloudflare DNS / Distributed high availability failover, written in cross-platform C# [.NET](https://github.com/dotnet) (Linux, Windows and macOS supported).

# Why is it useful? 🚀
Cloudflare [guarantees](https://www.cloudflare.com/dns/) the following for **free**:
> Our authoritative DNS is the fastest in the world, offering DNS lookup speed of 11ms on average and worldwide DNS propagation in less than 5 seconds.

Thus, this mechanism can be used to avoid a single point of failure ([SPoF](https://en.wikipedia.org/wiki/Single_point_of_failure)) of your network service (website, etc.). This is completely legal and in accordance with Cloudflare rules (using their official API).
Also a bonus are important security features: protection from DDoS and hiding the real IP of your servers.

# Who is it suitable for?
This solution is perfect for small and medium-sized projects that are not ready to spend huge resources (including financial) to maintain high availability of their services. If you want to provide HA fast and simple then this solution is for you.

# Why not use Cloudflare's built-in load balancer?
Your right, not a bad option. Given that this load balancer can also be used for transparent failover of your servers.
However, first of all, this feature is not available for the free Cloudflare plan :)

In addition, the following can be noted:
1. Even if you buy Cloudflare load balancer, you have to pay extra to use the checker logic flexibly and without restrictions (and something is available only for enterprise plan);
2. You don't fully control where your services availability checking comes from;
3. Only http(s) traffic balancing is possible (or you have to use enterprise plan);
4. You use one more intermediate node from your service to the end client;
5. You don't have flexible notifications to different applications/services.

**Attention!** In spite of everything, we ❤️ and respect Clouflare. Without it, this project would not be possible. 

# What features does dhaf have?
- Failover (dhaf will automatically and transparently switch your servers to end clients if necessary);
- Switchover (manually switching your servers) — useful for debugging and performing server maintenance invisibly to the end client;
- Support multiple services at once (which may "located" in subdomains of your domain name, for example);
- Email/telegram notifications about the health of your services or dhaf cluster;
- Flexible customization of the health checks of your service;
- Configuration in YAML format;
- Only you decide from where to check the availability of your service.

# Prerequisites
The following is recommended for stable operation of this solution:
1. Two servers of your entry point (master, replica) in independent data centers. This can be both load balancers (such as haproxy) and directly your services;
2. Three watchdog servers in independent datacenters. These can be the **cheapest** virtual servers (including cloud servers), because the load on them is minimal. The following should be installed on them:
    - **dhaf** (current project);
    - **[etcd](https://github.com/etcd-io/etcd)** >= v3.5 as DCS (Distributed Configuration Store).

# Quick Start
Below is a simple example of how to make dhaf work.
1. Suppose you have two similar servers in different data centers, which both provide your web-service. They have the following IP addresses: 111.111.111.11 (master) and 222.222.222.222 (replica);
1. Let us also assume that you have prepared three observer servers in different datacenters. They have the following IP addresses: 111.1.1.1, 112.2.2.2, 113.3.3.3;
1. Install and start etcd on all the watchdog servers (if you have not already done so). See details [here](https://etcd.io/docs/v3.5/quickstart/) and [here](https://etcd.io/docs/v3.5/op-guide/clustering/);
1. Install dhaf (requires .NET >= 5.0) from sources;
3. Create a Cloudflare account with a free plan (this will be enough). Transfer there DNS management for your domain name;
4. Create a configuration file config.dhaf, which has the following contents:
```yaml
cloudflare-api-token: <token>

init:
  dhaf-cluster-name: dhaf-cluster
  dhaf-node-name: dhaf-node1
  etcd:
    hosts: 111.1.1.1:2379,112.2.2.2:2379,113.3.3.3:2379 

services:
  foo:
    domain: foo.com
    hosts:
      master: 111.111.111.11
      replica: 222.222.222.222
    
    health-check:
      type: https
```
1. As you can see from the value of the "dhaf-node-name" parameter of the configuration file above, it is intended for the first server (hereinafter referred to as nodes). Create two more of these, replacing the value of the parameter "dhaf-node-name" with node2 and node3 respectively;
1. The only thing left to do is to run dhaf on all watchdog servers:
```shell
./dhaf --run config.dhaf
```
1. After the dhaf cluster is fully initialized, you can see its status:
```shell
./dhaf --status
```
1. Congratulations! Everything works. And now you can sleep well or test failures of your servers as an experiment.

# Available commands:
- `dhaf --run <config_file>` - start dhaf cluster node using configuration file <config_file>;
- `dhaf --status` - find out dhaf cluster status;
- `dhaf --switchover master|replica` - manually switch to master or replica.
- `dhaf --help` - show help.

# Configuration file
Major part:
|Parameter name|Type|Description|
| - | :-: | - |
| `cloudflare-api-token` | string | Your API access token for Cloudflare.|
| `init.dhaf-cluster-name` | string | Dhaf cluster-name. The characters a-zA-Z0-9 and - (hyphen) are allowed. |
| `init.dhaf-node-name` | string | The name of the current dhaf cluster node. The characters a-zA-Z0-9 and - (hyphen) are allowed. |
| `etcd.hosts` | string | Etcd hosts in the format `ip1:port1,ip2:port2,...,ipN:portN`. |
| `services.<name>`| key | Server configuration `<name>`.|
| `services.<name>.domain` | string | Domain name for service <name>. For example, `site.com`. |
| `services.<name>.hosts.master` | string | IP address of the master server. Will be used immediately if available. |
| `services.<name>.hosts.replica` | string | IP address of replica server. Will be used immediately if the master server is unavailable. |
| `services.<name>.health-check.type` | string | Checker type. Now `http` and `https` are available. |

The following optional parameters are also available when using http(s) checkers:
|Parameter name|Type|Description|Default|
| - | :-: | - | - |
| `services.<name>.health-check.method` | string | HTTP method that the checker uses. Now `GET` and `POST` are available. | GET |
| `services.<name>.health-check.port` | int | The port to health check against. | 80 (http), 443 (https) |
| `services.<name>.health-check.path` | string | The endpoint path to health check against. | / |
| `services.<name>.health-check.interval` | int | How often the checker should perform checks (in seconds). It is counted after the completion of the last check. Must be in the range 1-3600 seconds. | 30 |
| `services.<name>.health-check.timeout` | int | Timeout (in seconds) for connecting to the host to be checked. Must be in the range 1-86400 seconds. | 5 |
| `services.<name>.health-check.retries` | int | The number of retries to attempt in case of a timeout before marking the origin as unhealthy. Retries are attempted immediately. | 2 |
| `services.<name>.health-check.expected-codes` | string | The expected HTTP response code or code range of the health check. It is allowed to use X as a wildcard (e.g.: 2XX). Must indicate valid HTTP response code(s). | 200 |
| `services.<name>.health-check.response-body` | string | A case insensitive sub-string to look for in the response body. If this string is not found, the server will be marked as unhealthy. | Not set |
