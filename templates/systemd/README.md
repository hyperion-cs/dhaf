1. Create systemd service file (it's standard path for the most Linux distros, but you should check it before):
```shell
nano /etc/systemd/system/dhaf.service

```
2. Edit this basic service (especially paths and params):
```shell
[Unit]
Description=Dhaf
After=network.target

[Service]
Type=simple
WorkingDirectory=/opt/dhaf
ExecStart=/opt/dhaf/dhaf.node -c config-n1.dhaf
Restart=on-failure

[Install]
WantedBy=multi-user.target

```
3. Reload daemons:
```shell
systemctl daemon-reload

```
4. Test fresh Dhaf service:
```shell
systemctl restart dhaf.service
# Check status, it should be active
systemctl status dhaf.service

```
5. Enable it, to autostart service after reboot:
```shell
systemctl enable dhaf.service

```
