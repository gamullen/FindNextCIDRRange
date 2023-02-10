from azure.identity import ClientSecretCredential
from azure.mgmt.network import NetworkManagementClient
import os
import logging
from ipaddress import IPv4Network


def azure_authenticate():
    client_id = os.environ["ARM_CLIENT_ID"]
    client_secret = os.environ["ARM_CLIENT_SECRET"]
    tenant_id = os.environ["ARM_TENANT_ID"]
    credentials = ClientSecretCredential(
        client_id=client_id, client_secret=client_secret, tenant_id=tenant_id
    )
    return credentials


def find_available_subnet(
    subscription_id, resource_group_name, virtual_network_name, cidr
):
    subscription_id = os.environ["ARM_SUBSCRIPTION_ID"]
    api_version = "2022-11-01"
    success = False
    found_subnet = None
    http_status_code = None

    try:
        cidr = int(cidr)
        if cidr >= 0 and cidr <= 32:
            # Create a Network Management Client
            network_client = NetworkManagementClient(credentials=azure_authenticate(), subscription_id=subscription_id)

    except Exception as e:
        logging.error(f"Error has occured {e}")