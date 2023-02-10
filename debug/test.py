from azure.identity import ClientSecretCredential
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

subscription_id = os.environ["ARM_SUBSCRIPTION_ID"]
resource_group_name = "rg-ldo-euw-dev-build"
vnet_name = "vnet-ldo-euw-dev-01"


def azure_authenticate():
    client_id = os.environ["ARM_CLIENT_ID"]
    client_secret = os.environ["ARM_CLIENT_SECRET"]
    tenant_id = os.environ["ARM_TENANT_ID"]
    credentials = ClientSecretCredential(
        client_id=client_id, client_secret=client_secret, tenant_id=tenant_id
    )
    return credentials


def find_available_subnet(
    subscription_id, resource_group_name, new_subnet_size, virtual_network_name
):
    success = False

    try:
        # Checks that new subnet size is integer, and it is compliant with RFC standard
        new_subnet_size = int(new_subnet_size)
        if 0 <= new_subnet_size <= 32:
            # Create a Network Management Client
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
                    logging.error(
                        f"VNet {resource_group_name}/{virtual_network_name} cannot accept a "
                        f"subnet of size {new_subnet_size}"
                    )
                    exit(1)
                else:
                    # Get the subnets of the virtual network object return from Azure
                    subnets = [
                        IPv4Network(subnet.address_prefix)
                        for subnet in virtual_network.subnets
                    ]
                    logging.info(
                        f"The subnets within the {virtual_network_name} are {virtual_network.subnets}"
                    )
                    print(subnets)

                    # Calculate the possible subnets from the address space and subnet size asked for - gets a list
                    # of all IPNetworks with a /24 prefix
                    candidate_subnets = list(
                        network.subnets(new_prefix=new_subnet_size)
                    )
                    logging.info(
                        f"The possible {new_subnet_size} subnets available within {network} are: {candidate_subnets}"
                    )

                    for candidate_subnet in candidate_subnets:
                        logging.info(
                            f"Checking if {candidate_subnet} is being used within {vnet_name}"
                        )
                        if not success:
                            found_bad_subnet_in_candidate = False
                            for used_subnet in subnets:
                                logging.info(
                                    f"{used_subnet} is being used within {vnet_name}"
                                )
                                if (
                                    not found_bad_subnet_in_candidate
                                    and not used_subnet.overlaps(candidate_subnet)
                                ):
                                    suggested_available_subnet = (
                                        candidate_subnet.__str__()
                                    )
                                    logging.info(
                                        f"The suggested, next available subnet is {suggested_available_subnet}"
                                    )
                                    return suggested_available_subnet
                                else:
                                    found_bad_subnet_in_candidate = True
                            if found_bad_subnet_in_candidate:
                                success = False
                    if not success:
                        logging.error(
                            f"VNet {resource_group_name}/{virtual_network_name} cannot accept "
                            f"a subnet of size {new_subnet_size}"
                        )
                        exit(1)
        else:
            logging.error("Invalid CIDR size, must be between 0 and 32")
    except Exception as e:
        logging.error(f"Error has been encountered {e}")


result = find_available_subnet(
    subscription_id=subscription_id,
    resource_group_name=resource_group_name,
    virtual_network_name=vnet_name,
    new_subnet_size="24",
)

print(result)