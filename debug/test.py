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
            network_client = NetworkManagementClient(
                credentials=azure_authenticate(), subscription_id=subscription_id
            )

            # Get the virtual network
            virtual_network = next(
                (
                    x
                    for x in network_client.virtual_networks.list(resource_group_name)
                    if x.name == virtual_network_name
                ),
                None,
            )

            if virtual_network is None:
                logging.error(
                    "Virtual network {virtual_network_name} not found in resource group "
                    "{resource_group_name}"
                )
            else:
                # Check if the specified CIDR is within the range of the address prefixes of the virtual network
                address_prefixes = virtual_network.address_space.address_prefixes
                cidr_within_range = False
                for prefix in address_prefixes:
                    network = IPv4Network(prefix)
                    if cidr <= network.prefixlen:
                        cidr_within_range = True
                        break

                if not cidr_within_range:
                    logging.error(
                        f"VNet {resource_group_name}/{virtual_network_name} cannot accept a "
                        f"subnet of size {cidr}"
                    )
                    http_status_code = 404
                else:
                    # Get the subnets of the virtual network
                    subnets = [
                        IPv4Network(x.address_prefix) for x in virtual_network.subnets
                    ]

                    # Check if a subnet with the specified CIDR is available
                    candidate_subnets = list(network.subnets(cidr))
                    for candidate_subnet in candidate_subnets:
                        if not success:
                            found_bad_subnet_in_candidate = False
                            for used_subnet in subnets:
                                if (
                                    not found_bad_subnet_in_candidate
                                    and not used_subnet.overlaps(candidate_subnet)
                                ):
                                    success = True
                                    found_subnet = candidate_subnet.__str__()
                                else:
                                    found_bad_subnet_in_candidate = True
                            if found_bad_subnet_in_candidate:
                                success = False
                                found_bad_subnet_in_candidate = False
                                http_status_code = 404

                    if not success:
                        http_status_code = 404
                        logging.error(
                            f"VNet {resource_group_name}/{virtual_network_name} cannot accept a subnet of size {cidr}"
                        )
        else:
            logging.error("Invalid CIDR size, must be between 0 and 32")
    except Exception as e:
        logging.error(f"Error has been encountered {e}")
