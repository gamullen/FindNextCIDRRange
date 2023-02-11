from azure.identity import DefaultAzureCredential
from azure.mgmt.network import NetworkManagementClient
from ipaddress import IPv4Network
import azure.functions as func
import logging
import datetime


def main(findnextcidr: func.HttpRequest) -> func.HttpResponse:
    utc_timestamp = (
        datetime.datetime.utcnow().replace(tzinfo=datetime.timezone.utc).isoformat()
    )

    credential = DefaultAzureCredential()


def find_available_subnet(
    subscription_id, resource_group_name, virtual_network_name, new_subnet_size
):
    credential = DefaultAzureCredential()

    try:
        # Checks that new subnet size is integer, and it is compliant with RFC standard
        new_subnet_size = int(new_subnet_size)
        if 0 <= new_subnet_size <= 32:
            # Create a Network Management Client
            network_client = NetworkManagementClient(
                credential, subscription_id=subscription_id
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
                return func.HttpResponse(
                    "Virtual network {virtual_network_name} not found in resource group "
                    "{resource_group_name}",
                    status_code=400,
                )

            else:
                # Check if the specified CIDR is within the range of the address prefixes of the virtual network
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

                    return func.HttpResponse(
                        f"VNet {resource_group_name}/{virtual_network_name} cannot accept a "
                        f"subnet of size {new_subnet_size}",
                        status_code=400
                    )
                else:
                    # Get the subnets of the virtual network object return from Azure
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
                            return func.HttpResponse(
                                subnet.__str__(),
                                status_code=200
                            )
                    return func.HttpResponse(
                        status_code=404
                    )
        else:
            logging.error("Invalid CIDR size, must be between 0 and 32")
            return func.HttpResponse(
                "Invalid CIDR size, must be between 0 and 32",
                status_code=400
            )
    except Exception as e:
        logging.error(f"Error has been encountered {e}")
        return func.HttpResponse(
            f"Error has been encountered {e}",
            status_code=400
        )
