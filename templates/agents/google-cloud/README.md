# Google Cloud Agents
⚠️ Before reading the current document, read the main [README.md](../../README.md#with-google-cloud-switcher-provider) (in the "Quick Start/With Google Cloud switcher provider" section).

So, here are the agents that allow you to make a Google Cloud VM instance a reverse proxy that proxies to the server specified in the GC project metadata.
In general, you can implement this task in any way you want. However, we offer ready-made templates below.

## Nginx agent
Look at the nginx [subdirectory](nginx). Two files can be found there:
1. `nginx_template.conf` - a basic configuration for nginx that makes it a reverse proxy to the server specified in the static variable `$dhaf_ip`;
2. `dhaf_serv1_ip` - the file that contains the definition of the variable `$dhaf_ip`. It will be statically included to the main configuration file above.

Then look at the `gc-nginx-agent.py` file in the current directory.
It is a simple python (v3) script that monitors changes in Google Cloud project metadata, and,
if necessary, passes those changes to nginx (and also reloads its configuration). In other words, it fills the `dhaf_serv1_ip` file described above (and similar).
This does not require you to prepare any credentials, because inside the VM instance, project metadata is available by default
Aslo, it supports working for multiple services in dhaf at once.

In the directory with the script you must place the file `services.yaml`, inside which is a collection where each item contains:
1. Service name. Does not have a strong importance, implemented simply for usability;
2. Goolge Cloud Metadata key. The same key must be specified in the dhaf service configuration;
3. The path to the nginx subconfig (similar to `dhaf_serv1_ip` described above) which will contain the current ip address variable (`$dhaf_ip`) definition for proxying.

For example:
```yaml
- name: serv1
  gcmd_key: dhaf_serv1_ip
  nginx_subconfig_path: /etc/nginx/dhaf/dhaf_serv1_ip
```

That's all. Now you can make this script run with the OS, for example using `systemd`. You can find a ready-made template [here](systemd).

Oh yes, if you want to use the autoscaling feature of the [managed instance group](https://cloud.google.com/compute/docs/instance-groups#managed_instance_groups) (it's generally not necessary,
you can leave a static number of instances and disallow automatic changes),
don't forget to describe in the Google Cloud instance template the [startup-script](https://cloud.google.com/compute/docs/instance-templates/deterministic-instance-templates)
for installation of nginx and the corresponding agent (and so on). You can also use a container (e.g. a [docker](https://www.docker.com/)) for this. Good luck ;)
