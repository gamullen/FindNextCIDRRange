import logging
from ipaddress import IPv4Network
from azure.mgmt.network import NetworkManagementClient
from azure.identity import DefaultAzureCredential
import azure.functions as func
import traceback


def find_available_subnet(
    subscription_id: str,
    resource_group_name: str,
    virtual_network_name: str,
    new_subnet_size: int,
):
    try:
        if 0 <= new_subnet_size <= 32:
            network_client = NetworkManagementClient(
                DefaultAzureCredential(), subscription_id=subscription_id
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


def main(findnextcidr: func.HttpRequest) -> func.HttpResponse:
    try:
        subscription_id = findnextcidr.params.get("subscription_id")
        resource_group_name = findnextcidr.params.get("resource_group_name")
        virtual_network_name = findnextcidr.params.get("virtual_network_name")
        new_subnet_size = int(findnextcidr.params.get("new_subnet_size"))

        result = find_available_subnet(
            subscription_id=subscription_id,
            resource_group_name=resource_group_name,
            virtual_network_name=virtual_network_name,
            new_subnet_size=new_subnet_size,
        )
    except Exception as e:
        error = str(e) + "\n" + traceback.format_exc()
        return func.HttpResponse(f"An error has occured within the app: {error}", status_code=500)
    return func.HttpResponse(result)
