from azure.identity import DefaultAzureCredential, ClientSecretCredential
from azure.mgmt.resource import ResourceManagementClient
import azure.functions as func
import os
import logging
import datetime


def azure_authenticate():
    client_id = os.environ["ARM_CLIENT_ID"]
    client_secret = os.environ["ARM_CLIENT_SECRET"]
    tenant_id = os.environ["ARM_TENANT_ID"]
    credentials = ClientSecretCredential(
        client_id=client_id, client_secret=client_secret, tenant_id=tenant_id
    )
    return credentials


def propose_subnet(virtual_network_name, resource_group_name, cidr_string):
    subscription_id = ""
    arm_client = ArmClient(subscription_id, DefaultAzureCredential())
    v_nets = arm_client.get_default_subscription().get_virtual_networks()

    v_net = None
    error_message = None
    http_status_code = None
    error = None
    success = False
    found_bad_subnet_in_candidate_cidr = False
    found_subnet = None
    proposed_subnet_response = {}

    try:
        if cidr_string:
            cidr = int(cidr_string)
            if cidr > 0 and cidr <= 32:
                for v_net2 in v_nets:
                    if virtual_network_name == v_net2.data.name and resource_group_name == v_net2.id.resource_group_name:
                        v_net = v_net2
                        break

                if not v_net:
                    error_message = "Virtual network {} not found in resource group {}".format(virtual_network_name,
                                                                                               resource_group_name)
                else:
                    proposed_subnet_response["name"] = virtual_network_name
                    proposed_subnet_response["id"] = v_net.id
                    proposed_subnet_response["type"] = v_net.id.resource_type
                    proposed_subnet_response["location"] = v_net.data.location

                    v_net_cidrs = {}
                    for ip in v_net.data.address_space.address_prefixes:
                        v_net_cidr = IPNetwork(ip)
                        if cidr >= v_net_cidr.cidr:
                            v_net_cidrs[hash(v_net_cidr)] = v_net_cidr

                    for candidate_cidr in v_net_cidrs.values():
                        candidate_subnets = candidate_cidr.subnet(cidr)
                        used_subnets = []

                        used_subnets_azure = v_net.get_subnets()

                        for used_subnet in used_subnets_azure:
                            used_subnets.append(IPNetwork(used_subnet.data.address_prefix))

                        for candidate_subnet in candidate_subnets:
                            if not success:
                                found_bad_subnet_in_candidate_cidr = False
                                for used_subnet in used_subnets:
                                    if not found_bad_subnet_in_candidate_cidr and not used_subnet.overlaps(
                                            candidate_subnet):
                                        success = True
                                        found_subnet = str(candidate_subnet)
                                    else:
                                        found_bad_subnet_in_candidate_cidr = True
