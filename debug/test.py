from azure.identity import ClientSecretCredential, DefaultAzureCredential
from azure.mgmt.network import NetworkManagementClient
from ipaddress import IPv4Network
import os
import logging

# Set logging level. .DEBUG, .WARNING, .INFO etc
logging.basicConfig(level=logging.WARNING)

# create a stream handler and set its level to logging.DEBUG
console_handler = logging.StreamHandler()
console_handler.setLevel(logging.DEBUG)

# add the stream handler to the root logger
root_logger = logging.getLogger()
root_logger.addHandler(console_handler)

sub_id = os.environ["ARM_SUBSCRIPTION_ID"]
rg_name = "rg-ldo-euw-dev-build"
vnet_name = "vnet-ldo-euw-dev-01"


def azure_authenticate():
    try:
        client_id = os.environ["ARM_CLIENT_ID"]
        client_secret = os.environ["ARM_CLIENT_SECRET"]
        tenant_id = os.environ["ARM_TENANT_ID"]
        credentials = ClientSecretCredential(
            client_id=client_id, client_secret=client_secret, tenant_id=tenant_id
        )
        return credentials
    except Exception as e:
        logging.error(f"Error encountered when trying to authenticate to Azure: {e}")


def find_available_subnet(
    subscription_id: str,
    resource_group_name: str,
    virtual_network_name: str,
    new_subnet_size: int,
):
    try:
        if 0 <= new_subnet_size <= 32:
            network_client = NetworkManagementClient(
                azure_authenticate(), subscription_id=subscription_id
            )

            list_virtual_networks = network_client.virtual_networks.list(
                resource_group_name
            )

            virtual_network = None
            for vnet in list_virtual_networks:
                if virtual_network_name == vnet.name:
                    virtual_network = vnet
                    break

            if virtual_network is None:
                logging.error(
                    "Virtual network {virtual_network_name} not found in resource group "
                    "{resource_group_name}"
                )
            else:
                address_prefixes = virtual_network.address_space.address_prefixes
                cidr_within_range = False
                for prefix in address_prefixes:
                    network = IPv4Network(prefix)
                    network_prefix = network.prefixlen
                    if new_subnet_size >= network_prefix:
                        cidr_within_range = True
                        logging.info(
                            f"CIDR is within range as {new_subnet_size} is a smaller or"
                            f" same sized subnet than {network_prefix}"
                        )
                        break

                if not cidr_within_range:
                    logging.error(
                        f"VNet {resource_group_name}/{virtual_network_name} cannot accept a "
                        f"subnet of size {new_subnet_size}"
                    )
                else:
                    used_subnets = [
                        IPv4Network(subnet.address_prefix)
                        for subnet in virtual_network.subnets
                    ]
                    logging.info(
                        f"The subnets used within the {virtual_network_name} are {virtual_network.subnets}"
                    )
                    used_subnets = sorted(used_subnets)
                    for subnet in network.subnets(new_prefix=new_subnet_size):
                        if not any(
                            subnet.subnet_of(used) for used in used_subnets
                        ) and not any(subnet.overlaps(used) for used in used_subnets):
                            return subnet.__str__()
                    return None
        else:
            logging.error("Invalid CIDR size, must be between 0 and 32")
    except Exception as e:
        logging.error(f"Error has been encountered {e}")


result = find_available_subnet(
    subscription_id=sub_id,
    resource_group_name=rg_name,
    virtual_network_name=vnet_name,
    new_subnet_size=24,
)

print(result)
