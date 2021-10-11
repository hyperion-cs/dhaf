import os
import requests
import asyncio
import functools
import logging
import yaml

with open('services.yaml') as f:
    services = yaml.safe_load(f)

TACT_TIMEOUT = 30
APPLY_CHANGES_COMMAND = "systemctl reload nginx"
EVENT_LOOP = asyncio.get_event_loop()

logging.basicConfig(
    format='%(asctime)s %(levelname)s: %(message)s', level=logging.INFO)


def GET_NGINX_SUBCONFIG_CONTENT(ip): return f"set $dhaf_ip \"{str(ip)}\";"


async def get_gc_metadata(md_key, timeout=0):
    path = f"http://metadata.google.internal/computeMetadata/v1/project/attributes/{md_key}"

    if timeout > 0:
        path = path + f"?wait_for_change=true&timeout_sec={TACT_TIMEOUT}"

    request_task = EVENT_LOOP.run_in_executor(None, functools.partial(
        requests.get, path, headers={"Metadata-Flavor": "Google"}))
    resp = await request_task

    if resp.status_code == 200:
        return resp.content.decode('UTF-8')
    else:
        return None


async def watch_gc_metadata(service_name, gcmd_key, nginx_subconfig_path):
    prev_value = await get_gc_metadata(gcmd_key)
    logging.info(f"Start watch [{service_name}] (init value: {prev_value})")
    while True:
        curr_value = await get_gc_metadata(gcmd_key, TACT_TIMEOUT)

        if curr_value is not None and curr_value != prev_value:
            logging.warning(
                f"[{service_name}] Changed the value <{prev_value}> → <{curr_value}>.")

            prev_value = curr_value
            curr_content = GET_NGINX_SUBCONFIG_CONTENT(prev_value)

            try:
                with open(nginx_subconfig_path, "w") as f:
                    f.write(curr_content)

                os.system(APPLY_CHANGES_COMMAND)
                logging.info(f"[{service_name}] Changes applied.")

            except Exception as e:
                logging.error(str(e))

if os.geteuid() != 0:
    logging.critical("The \"google cloud nginx agent\" must be run as root.")
    exit()

logging.info("The \"google cloud nginx agent\" started...")
tasks = [watch_gc_metadata(
    i["name"], i["gcmd_key"], i["nginx_subconfig_path"]) for i in services]

EVENT_LOOP.run_until_complete(asyncio.wait(tasks))
EVENT_LOOP.close()
