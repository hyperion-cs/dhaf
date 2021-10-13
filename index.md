![project_identity](https://raw.githubusercontent.com/hyperion-cs/dhaf/main/project_identity/github.png)
# Dhaf
[![linux-x64](https://github.com/hyperion-cs/dhaf/actions/workflows/linux-x64.yml/badge.svg)](https://github.com/hyperion-cs/dhaf/actions/workflows/linux-x64.yml)
[![win-x64](https://github.com/hyperion-cs/dhaf/actions/workflows/win-x64.yml/badge.svg)](https://github.com/hyperion-cs/dhaf/actions/workflows/win-x64.yml)
[![macOS-x64](https://github.com/hyperion-cs/dhaf/actions/workflows/osx-x64.yml/badge.svg)](https://github.com/hyperion-cs/dhaf/actions/workflows/osx-x64.yml)

Distributed high availability failover, written in cross-platform C# [.NET](https://github.com/dotnet) (Linux, Windows and macOS supported).

You can [join](https://t.me/dhaf_chat) a Telegram chat to discuss **dhaf**.

# Why is it useful? 🚀
Dhaf is a system that keeps your web service **always online** for the end user. It's available to everyone for free and without the need for special knowledge or complicated network infrastructure.

Dhaf has switchers - various providers to manage entry points of web-service, health checkers and notifiers.
It is extremely flexible, extensible and easy to use and configure.

To avoid bogging you down with details right away, let's take a look at one of the great use cases below.

One of the switchers implemented in Dhaf is the Cloudflare provider. This company [guarantees](https://www.cloudflare.com/dns/) the following for **free**:
> Our authoritative DNS is the fastest in the world, offering DNS lookup speed of 11ms on average and worldwide DNS propagation in less than 5 seconds.

Thus, this mechanism can be used to avoid a single point of failure ([SPoF](https://en.wikipedia.org/wiki/Single_point_of_failure)) of your network service (website, etc.). This is completely legal and in accordance with Cloudflare rules (using their official API).
Also a bonus are important security features: protection from DDoS and hiding the real IP of your servers.

\* However, this is not the only option. For example, you can use `Google Cloud` switcher provider or `Exec`.

# Who is it suitable for?
This solution is perfect for small and medium-sized projects that are not ready to spend huge resources (including financial) to maintain high availability of their services. If you want to provide HA fast and simple then this solution is for you.

# What major features does dhaf have?
- Failover (dhaf will automatically and transparently switch your servers to end clients if necessary);
- Switchover (manually switching your servers) — useful for debugging and performing server maintenance invisibly to the end client;
- Support for several different services in one dhaf cluster (which may "located" in subdomains of your domain name, for example);
- Flexible notifier providers. Notifications are already available for:
    - Email
    - Telegram Messenger
- Flexible customization of the health checks of your service;
- All user configuration in YAML format;
- Only you decide from where to check the availability of your services;
- User-friendly CLI for check and manage your dhaf cluster;
- REST API for check and manage your dhaf cluster;
- Feature to easily create your own providers for switchers, health checkers and notifiers in C#;
- Feature to connect own executable scripts (e.g., in Python) as switchers and health checkers;
- High availability and distribution due to the fact that dhaf is a cluster.

# Prerequisites
The following is recommended for stable operation of this solution:
1. Two servers of your entry point (primary, secondary) in independent data centers. This can be both load balancers (such as haproxy) and directly your services;
2. Three dhaf servers in independent datacenters. These can be the **cheapest** virtual servers (including cloud servers), because the load on them is minimal. The following should be installed on them:
    - **dhaf** (current project);
    - **[etcd](https://github.com/etcd-io/etcd)** >= v3.5 as DCS (Distributed Configuration Store);
    -  .NET Runtime >= 5.0 (download [here](https://dotnet.microsoft.com/download/dotnet/5.0/runtime)).

# Quick Start
### With Cloudflare switcher provider
\* The SLA [promises](https://www.cloudflare.com/business-sla/) 100% uptime.

1. Suppose you have two similar servers in different data centers, which both provide your web-service:
    |Name|IP|Priority|
    | :-: | :-: | :-: |
    |serv1|111.111.111.11|1|
    |serv2|222.222.222.22|2|
2. Let us also assume that you have prepared three dhaf servers in different datacenters:
    |Name|IP|
    | :-: | :-: |
    |d1|111.1.1.1|
    |d2|112.2.2.2|
    |d3|113.3.3.3|
3. Install and start etcd on all the dhaf servers (if you have not already done so). See details [here](https://etcd.io/docs/v3.5/quickstart/) and [here](https://etcd.io/docs/v3.5/op-guide/clustering/).
4. Download and install our [dhaf builds](https://github.com/hyperion-cs/dhaf/releases) or build from sources (requires .NET >= 5.0) on all the dhaf servers;
5. Create a Cloudflare account with a free plan (this will be enough). Transfer there DNS management for your domain name `foo.com`. Also note that your domain name must have only one A record in the DNS that has the Clouflare proxying checkbox checked. Otherwise, you will get [round-robin](https://en.wikipedia.org/wiki/Round-robin_DNS) detrimental for our purposes and/or unacceptably slow updating of DNS records for end clients;
    - ⚠️ Warning! To combat scammers, Cloudflare does not allow DNS configuration via the official API for domains with a .cf, .ga, .gq, .ml, or .tk TLD (top-level domain). Thus, it is not possible to work with them in **dhaf** either. However, it is still possible to manually configure them in Cloudflare Dashboard.
6. Using the Clouflare dashboard, [create](https://dash.cloudflare.com/profile/api-tokens) an API token (if you have not already done so) with access to edit the DNS records of your domain zone. You also need to set an adequate TTL (lifetime) of your token, and keep it up to date.
7. For the **first** dhaf node, сreate a configuration file `config-n1.dhaf`, which has the following contents:
```yaml
dhaf:
  cluster-name: first-dhaf-cluster
  node-name: node-1

etcd:
  hosts: http://111.1.1.1:2379,http://112.2.2.2:2379,http://113.3.3.3:2379

services:
  - name: main
    domain: foo.com
    entry-points:
      - name: ep-1
        ip: 111.111.111.11
      - name: ep-2
        ip: 222.222.222.22
    switcher:
      type: cloudflare
      api-token: <your_api_token>
      zone: foo.com
    health-checker:
      type: web
      schema: http
```
8. As you can see from the value of the `dhaf.node-name` parameter of the configuration file above, it is intended for the first dhaf node. Create two more of these, replacing the value of the parameter `dhaf.node-name` with `node-2` and `node-3` respectively (as well as the name of the config so as not to get confused);
9. The only thing left to do is to run dhaf on all dhaf servers (don't forget to substitute the appropriate configuration file):
```shell
./dhaf.node --config config-n1.dhaf
```
10. After the dhaf cluster is fully initialized, you can see its status via CLI:
```shell
./dhaf.cli status-services --config config-n1.dhaf
```
11. Congratulations! Everything works. And now you can test failures of your servers as an experiment.

### With Google Cloud switcher provider
\* The SLA [promises](https://cloud.google.com/compute/sla) >= 99.99% monthly uptime.
This provider may be needed for those who for some reason do not want to deal with Cloudflare. Note that Google Cloud services are **paid** (although very cheap for our purposes), unlike other switcher providers. Also, this method is more complicated than the others. The **dhaf** itself, of course, is always free.

The idea is that with the Google Cloud / Compute Engine we will create a high availability entry point (hereinafter referred to as Google Cloud gateway) into your web service that can proxy end user requests to your own balancers/backends/etc. With **dhaf**, it will be possible to immediately switch entry points to healthy/relevant ones. The Google Cloud gateway has a static IP address, which can be specified in your DNS records (and will not need to be changed).

The Google Cloud gateway implementation is based on several [VM](https://cloud.google.com/compute/docs/instances) instances in multiple zones with TCP load balancer ([Layer 4](https://developers.google.com/compute/docs/load-balancing/network/?hl=en_US)). Consistent state among VM instances is guaranteed through the use of [metadata](https://cloud.google.com/compute/docs/metadata/overview) at the level of the entire Google Cloud project (this is a simple key-value storage that is available inside each VM instance). The load balancer has a static IP address and is a Google Cloud gateway.

Thus, each VM instance performs a reverse proxy function to the server specified in the project metadata, and the load balancer ensures constant availability to the VM instances. Even if a few of the VM instances go down, which is unlikely (each instance is [guaranteed](https://cloud.google.com/compute/sla) to have >= 99.5% monthly uptime), the gateway will still continue to work. As stated above, the gateway monthly uptime is >=99.99%.

What remains to be understood is how to implement it. In fact, it's up to you to decide. However, for a start, below we offer a working variant (template), which you can change to suit you. The main thing is to ensure that the principles described above are followed. So, you should do the following in Google Cloud:
1. Create a VM [instance template](https://cloud.google.com/compute/docs/instance-templates) (_Compute Engine → Virtual Machines → Instance templates_ in GC Console). Each instance can be the cheapest machine (e.g. `e2-micro` with 2 vCPUs, 1 GB memory), since reverse proxying does not take much resources. A Linux-like operating system (e.g. Debian or Ubuntu) is highly recommended as a distribution. How to provide reverse proxying on each instance will be described below;
2. Create a **managed** group of VM instances / [MIG](https://cloud.google.com/compute/docs/instance-groups#managed_instance_groups) (_Compute Engine → Instance groups → Instance groups_ in GC Console) in **different** [zones](https://cloud.google.com/compute/docs/regions-zones) of the same region based on instance template. The IP addresses of the VM instances can be [ephemeral](https://cloud.google.com/compute/docs/ip-addresses#ephemeraladdress) — it does not matter. There must be two or three VM instances in group at least;
3. Create [health checks](https://cloud.google.com/compute/docs/load-balancing/health-checks) (_Compute Engine → Instance groups → Health checks_ in GC Console) for the managed instance group created above. This could be, for example, HTTP health checks (if you provide endpoints on the VM instances for this). The health criteria should be strict (e.g., 10 seconds for check interval and 5 seconds timeout);
4. Create a [TCP load balancer](https://cloud.google.com/load-balancing/docs/tcp) (_Networking → Network services → Load balancing_ in GC Console) based on the managed instance group created above. It must have a [premium](https://cloud.google.com/network-tiers/docs/using-network-service-tiers) static IP address. This will act as a Google cloud gateway;
5. In the A-record of the DNS domain of your web service, write the IP address of the Google cloud gateway (the load balancer created by the step above).

So, a high availability entry point has been created. However, as mentioned above, each VM instance must act as a reverse proxy to the server specified in the Google Cloud project metadata. How to do it is also up to you. You can use [haproxy](https://en.wikipedia.org/wiki/HAProxy)/[nginx](https://en.wikipedia.org/wiki/Nginx)/[envoy](https://www.envoyproxy.io/)/etc. We propose to use nginx as the easiest solution. See the template for getting started [here](templates/agents/google-cloud).

The **dhaf** configuration in the case of the current provider is similar to the configuration with, for example, Cloudflare. However, the swither section in the service will look different. For example, like this:
```yaml
switcher:
  type: google-cloud
  project: gcprj
  metadata-key: dhaf-serv1-ip
  credentials-path: gcprj-16b1a111a555.json
```
**Dhaf** will manage the values in the project metadata itself.

# What are the benefits of dhaf being a cluster?
Well, such as these:
- If one of the dhaf cluster nodes fails, the cluster itself will still continue to work (as long as possible). If the leader fails, a so-called race for the leader will immediately [begin](https://en.wikipedia.org/wiki/Leader_election), i.e. a new leader will be determined by the "first come, first served" method;
- Health checks for the web service are performed by all available nodes, and the decision about (un)availability of it is collective. And only if the majority considers that the service is unavailable, the cluster leader starts the procedure to switch the entry point to the web service (in the case of Cloudflare's switcher provider, it changes the A-record in Cloudflare DNS to the next server). This elegantly solves the problem when one of the nodes has decided that the web service is unreachable, although in fact it is not true for real users.

# Available CLI commands:
- `./dhaf.cli status-dhaf --config <config_file>` - show dhaf cluster status information.
- `./dhaf.cli status-service --service <service_name> --config <config_file>` - show service status information.
- `./dhaf.cli status-service --config <config_file>` - show all services status information.
- `./dhaf.cli switchover-candidates --service <service_name> --config <config_file>` - show suitable entry points for switchover.
- `./dhaf.cli switchover-to <ep> --service <service_name> --config <config_file>` - switchover to the `<ep>` entry point.
- `./dhaf.cli switchover-purge --config <config_file>` - purge the switchover requirement.
- `./dhaf.cli node-decommission <node_name> -config <config_file>` - decommission the dhaf node `<node_name>`.
- `./dhaf.cli help` - display more information on a specific command.
- `./dhaf.cli version` - display version information.

# Available REST API endpoints:
Similar functionality to the CLI (because the CLI uses the REST API). Detailed documentation is in development, for now you can see [here](/src/Dhaf.Node/RestApi/RestApiController.cs).

# Available providers (core extensions)
|Type|Name|Description|
| :-: | :-: | - |
| switcher | cloudflare | Performs entry points switching by quickly changing Cloudflare DNS records. |
| switcher | google-cloud | Performs entry points switching via Google Cloud gateway. |
| switcher | exec | Switching entry points via an executable file (e.g. a Python script). |
| health checker | web | Checks the health of the http(s) service. |
| health checker | exec | Checks the health of service via an executable file (e.g. a Python script). |
| notifier | email | Email notifications from dhaf. |
| notifier | tg | Telegram messenger notifications from dhaf. |

Need another extension? Leave a [feature request](https://github.com/hyperion-cs/dhaf/issues).

# Configuration file
### Major part (required)
⚠️ All **names** are limited to 64 characters in length. Also, only the characters `a-zA-Z0-9`, `-` (hyphen) and `_` are allowed.

|Parameter name|Type|Description|
| - | :-: | - |
| `dhaf.cluster-name` | string | Dhaf cluster-name.  |
| `dhaf.node-name` | string | The name of the current dhaf cluster node. |
| `etcd.hosts` | string | Etcd hosts in the format `ip1:port1,ip2:port2,...,ipN:portN`. |
| `services` | list | The list of services that dhaf will keep available. |
| `services[].name` | string | The name of the service. |
| `services[].domain` | string | Domain name for service \<name\>. For example, `site.com`. |
| `services[].entry-points` | list | List of entry points for service \<name\> in **order** of priority. |
| `services[].entry-points[].name` | string | The name of the entry point. |
| `services[].entry-points[].ip` | string | The IP address of the entry point \<name\>. |
| `services[].switcher` | object | Switcher for service \<name\>. |
| `services[].switcher.type` | object | Name (provider type) of switcher for service \<name\>. |
| `services[].health-checker` | object | Health checker for service \<name\>. |
| `services[].health-checker.type` | object | Name (provider type) of health checker for service \<name\>. |
   
### Optional
|Parameter name|Type|Description|Default|
| - | :-: | - | :-: |
| `dhaf.healthy-node-status-ttl` | string | For how long the dhaf node is considered healthy after the last heartbeat. | `30` |
| `dhaf.heartbeat-interval` | string | Frequency of sending heartbeat of node dhaf to distributed storage (dcs). | `5` |
| `dhaf.tact-interval` | string | How often the dhaf should perform checks (in seconds). It is counted after the completion of the last check. Must be in the range 1-3600 seconds. | `10` |
| `dhaf.tact-post-switch-delay` | string | Delay before the next tact in the case of a failover/switchover/switching. Must be in the range 0-3600 seconds. | `0` |
| `dhaf.web-api` | string | REST API configuration for the dhaf node. | — |
| `dhaf.web-api.host` | string | REST API Host. | `localhost` |
| `dhaf.web-api.port` | int | Rest API Port | `8128` |
| `etcd.username` | string | Etcd authentication username. | — |
| `etcd.password` | string | Etcd authentication password. | — |

### Configurations for switchers providers
    
Cloudflare switcher provider (`cloudflare`):
|Parameter name|Type|Description|Default|
| - | :-: | - | :-: |
| `api-token` | string | API token for managing your DNS records in Cloudflare. | Required |
| `zone` | string | Zone for managing your DNS records in Cloudflare. | Required |
    
Google cloud switcher provider (`google-cloud`):
|Parameter name|Type|Description|Default|
| - | :-: | - | :-: |
| `project` | string | Google Cloud [project](https://cloud.google.com/resource-manager/docs/creating-managing-projects) ID. | Required |
| `metadata-key` | string | Google Cloud [metadata](https://cloud.google.com/compute/docs/metadata/overview) key. Dhaf uses this key to tell the Google Cloud gateway to switch to another entry point. | Required |
| `credentials-path` | string | Path to the *.json* file containing the Google Cloud [service account key](https://cloud.google.com/iam/docs/creating-managing-service-account-keys#creating_service_account_keys) (сan be specified relative to `dhaf.node`). This service account must have [access](https://cloud.google.com/compute/docs/access/iam) to project metadata management (_Compute Engine → Settings → Metadata_ in GC Console). The `roles/compute.instanceAdmin.v1` role is suitable. | Required |
    
Exec switcher provider (`exec`):
|Parameter name|Type|Description|Default|
| - | :-: | - | :-: |
| `init` | string | Path to the executable file for provider initialization. | Required |
| `switch` | string | Path to the executable file to switch. The command line arguments for switching will be passed: [entry point name, entry point ip].| Required |
    
### Configurations for health check providers

Web health check provider (`web`):
|Parameter name|Type|Description|Default|
| - | :-: | - | :-: |
| `schema` | string | Uri schema. Now `http` and `https` are available. | `http` |
| `method` | string | HTTP method that the checker uses. Now `GET` and `POST` are available. | `GET` |
| `port` | int | The port to health check against. | `80` (http), `443` (https) |
| `path` | string | The endpoint path to health check against. | `/` |
| `headers` | dict | Http headers in key-value format. | — |
| `follow-redirects` | bool | Is it necessary to follow redirects? | `false` |
| `timeout` | int | Timeout (in seconds) for connecting to the host to be checked. Must be in the range 1-86400 seconds. | `5` |
| `retries` | int | The number of retries to attempt in case of a timeout before marking the origin as unhealthy. Retries are attempted immediately. | `2` |
| `expected-codes` | string | The expected HTTP response code or code range of the health check in the format `111,222,3XX`. It is allowed to use X as a wildcard (e.g.: 2XX). Must indicate valid HTTP response code(s). | `200` |
| `expected-response-body` | string | A case insensitive sub-string to look for in the response body. If this string is not found, the server will be marked as unhealthy. | — |
| `domain-forwarding` | bool | Automatically forward the domain name of the service (`services.domain` parameter) to the `Host` header for each HTTP request. If the `Host` header is specified manually (via `headers` parameter), there will be **no forwarding** even if the current parameter is set to `true`. | `true` |
| `ignore-ssl-errors` | bool | Ignore SSL certificate validation errors (applies to `HTTPS` schema only). | `false` |
    
Exec health check provider (`exec`):
|Parameter name|Type|Description|Default|
| - | :-: | - | :-: |
| `init` | string | Path to the executable file for provider initialization. | Required |
| `check` | string | Path to the executable file to health check. The command line arguments for health check will be passed: [entry point name, entry point ip]. Should return exit code 0 if the entry point is considered healthy. | Required |
    
### Configurations for notifier providers
⚠️ Pay attention! There can be several of them in one cluster. However, they will be the same for all services.

E-Mail notifier provider (`email`):
|Parameter name|Type|Description|Default|
| - | :-: | - | :-: |
| name | string | Notifier instance name. | `email-anon` |
| from | string | The email address of the sender of the notifications. | Required |
| to | string | The email address of the recipient of the notifications. | Required |
| smtp | object | SMTP server configuration. | — |
| smtp.server | string | SMTP server host. | Required |
| smtp.port | int | SMTP server port. | Required |
| smtp.security | string | Connection security. Possible values: `ssl`. | — |
| smtp.username | string | SMTP server credentials -> username. | — |
| smtp.password | string | SMTP server credentials -> password. | — |
    
Telegram messenger notifier provider (`tg`):
|Parameter name|Type|Description|Default|
| - | :-: | - | :-: |
| name | string | Notifier instance name. | `tg-anon` |
| token | string | API token from Telegram bot. | Required |
| join-code | string | The security code that is required to start a notification bot in private messages or a group. | Required |
  
Telegram bot prerequisites:
- All you have to do is create a bot via @BotFather (more details [here](https://core.telegram.org/bots#3-how-do-i-create-a-bot)) and write the API key in the config. Everything else is taken care of by the `tg` provider (in other words, the `tg` provider acts as a server for the bot). It does NOT require incoming connections because it uses [long polling](https://en.wikipedia.org/wiki/Push_technology#Long_polling);
- It is worth turning on [privacy mode](https://core.telegram.org/bots#privacy-mode) in the settings of the bot.
    
# Building from sources
On UNIX-like systems, you can use [this .sh template](/templates/build_example.sh) to build (with this template you can build for any platform). However, please note that you will need the [.NET 5 SDK](https://dotnet.microsoft.com/download/dotnet/5.0) installed.
    
Looking at the above template, it is not difficult to perform a building from sources on platforms such as Windows and/or macOS also.
    
# Dhaf as a service, automatically started at OS startup
This is done e.g. via [Systemd](https://en.wikipedia.org/wiki/Systemd) on Linux. For Windows you can use [Windows service](https://en.wikipedia.org/wiki/Windows_service).
The templates can be found in the `templates` folder of the current repository.

# Some tips for dhaf cooking
- Do not host dhaf cluster nodes on the same servers as your web service;
- It is possible (and logical) to place the etcd nodes in the same place as dhaf;
- Number of nodes etcd should not be less than three (then it is possible to survive the fall of one of the nodes). Read more [here](https://etcd.io/docs/v3.5/faq/#what-is-failure-tolerance);
- The number of dhaf nodes should be at least two, but considering that they usually work in the same place as etcd, it is logical to make them three as well. However, even with a two-node cluster, dhaf can survive a failure of one of the nodes;
- For a dhaf cluster node (even in combination with etcd) the cheapest virtual server (including cloud servers) is enough, because the load on it is minimal due to the fact that it is not engaged in serving a bunch of users - it deals with the health of your web service;
- Locate dhaf cluster nodes (and etcd) at different providers in different data centers. This prevents you from having to deal with the unpleasant situation where one of the providers has its entire infrastructure down. Also, nodes should be geographically located where you expect to use your service (i.e. in terms of the network "bring" nodes closer to real users);
- The entry points for the web service in the dhaf configuration should be at least two, but perhaps this is obvious;
- If you have a desire and/or need to load balance, that is not what dhaf is for. Its purpose is to provide a working entry point into a web service, and it is by no means a (reverse) proxy server. So, if there is a need for the above, then the entry points in the dhaf configuration should be servers with e.g. [HAProxy](https://en.wikipedia.org/wiki/HAProxy) (or its equivalent), whose functionality is designed to do just that.

# How to write your own dhaf provider in .NET
Want to write your own provider (switcher or health checker) for **dhaf**? It's very easy to do. Out of the box in the dhaf core, such things as logging, distributed storage access (extension area only), configurations (internal and for users) and so on are available for extension development.
    
Minimal templates (skeletons) for each provider type can be seen [here](/templates/dhaf_extensions). If you think your extension turned out wonderful, you can create a [PR](https://github.com/hyperion-cs/dhaf/pulls) to add it to the core extensions.
    
All **dhaf** extensions have one required dependency — `Dhaf.Core`. For ease of development, this can be obtained directly from [NuGet](https://www.nuget.org/packages/Dhaf.Core/). Keep in mind, however, that this dependency should not be packed into your extension when publishing, since it is already present in `Dhaf.Node` (this is the dhaf component that uses your extensions). The same applies to the `Microsoft.Extensions.Logging.Abstractions`. So they both have to be added with the `PrivateAssets="All"` parameter to your **.csproj** extension:
```xml
<PackageReference Include="Dhaf.Core" Version="<VERSION_HERE>"  PrivateAssets="All"/>
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="<VERSION_HERE>" PrivateAssets="All" />
```
You can run (e.g., to manually test the extension) directly using `Dhaf.Node`. You can also set up automatic copying (for development purposes only) of your extension for `Dhaf.Node` into **.csproj**:
```xml
<Target Name="CopyExtension" Condition="'$(Configuration)' == 'Debug'" AfterTargets="AfterBuild">
    <Copy SourceFiles="$(OutDir)\<YOUR_EXT_BIN_NAME>.dll;$(OutDir)\extension.json"
          DestinationFolder="$(SolutionDir)\<PATH_TO_DHAF_NODE>\extensions\<YOUR_EXT_TYPE>\<YOUR_EXT_NAME>" />
</Target>
```
    
# Terminology
- Failover — emergency switching of the entry point in automatic mode;
- Switchover — knowingly manually switching entry points (for maintenance, testing, etc.);
- Switching — automatically switching of the entry point to the higher priority of the healthy ones.
    
