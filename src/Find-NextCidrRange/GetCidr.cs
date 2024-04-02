// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

/*
 MIT License
Copyright (c) 2021 Gary L. Mullen-Schultz
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Network;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace FindNextCIDR
{
    public static class GetCidr
    {
        public class ProposedSubnetResponse
        {
            public ProposedSubnetResponse()
            {
            }

            public string name { get; set; }
            public string id { get; set; }
            public string type { get; set; }
            public string location { get; set; }
            public string addressSpace { get; set; }
            public string proposedCIDR { get; set; }
        }
        public class CustomError
        {
            public string code { get; set; }
            public string message { get; set; }
        }

        static HttpStatusCode httpStatusCode = HttpStatusCode.OK;

        static Boolean foundDesiredAddressSpace = false;

        [FunctionName("GetCidr")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            ProposedSubnetResponse proposedSubnetResponse = new ProposedSubnetResponse();

            string subscriptionId = req.Query["subscriptionId"];
            string virtualNetworkName = req.Query["virtualNetworkName"];
            string resourceGroupName = req.Query["resourceGroupName"];
            string cidrString = req.Query["cidr"];
            string desiredAddressSpace = req.Query["addressSpace"];
            byte cidr = 0;

            Exception error = null;
            string errorMessage = null;
            bool success = false;
            bool foundBadSubnetInCandidateCIDR = false;
            string foundSubnet = null;
            string foundAddressSpace = null;

            try
            {
                // Validate the input params
                errorMessage = validateInput(subscriptionId, virtualNetworkName, resourceGroupName, cidrString, desiredAddressSpace);
                if (null == errorMessage)
                {
                    // Make sure the CIDR is valid
                    if (validateCIDR(cidrString))
                    {
                        cidr = Byte.Parse(cidrString);
                        VirtualNetworkResource vNet = null;

                        // Get a client for the SDK calls
                        var armClient = new ArmClient(new DefaultAzureCredential(), subscriptionId);
                        Azure.Pageable<VirtualNetworkResource> vNets = armClient.GetDefaultSubscription().GetVirtualNetworks();

                        // For each VNet find ours!
                        foreach (VirtualNetworkResource vNet2 in vNets)
                        {
                            // Find the desired VNet
                            if (virtualNetworkName.Equals(vNet2.Data.Name) && resourceGroupName.Equals(vNet2.Id.ResourceGroupName))
                            {
                                vNet = vNet2;
                            }
                        }

                        if (null == vNet)
                        {
                            errorMessage = "Virtual network " + virtualNetworkName + " not found in resource group " + resourceGroupName;
                        }
                        else
                        {
                            proposedSubnetResponse.name = virtualNetworkName;
                            proposedSubnetResponse.id = vNet.Id;
                            proposedSubnetResponse.type = vNet.Id.ResourceType;
                            proposedSubnetResponse.location = vNet.Data.Location;

                            Hashtable vNetCIDRs = new Hashtable();

                            foreach (string ip in vNet.Data.AddressPrefixes)
                            {
                                IPNetwork2 vNetCIDR = IPNetwork2.Parse(ip);
                                if (cidr >= vNetCIDR.Cidr)
                                {
                                    vNetCIDRs.Add(vNetCIDR.GetHashCode(), vNetCIDR);
                                }
                            }

                            //Go though every address space in the specified VNet
                            foreach (IPNetwork2 candidateCIDR in vNetCIDRs.Values)
                            {
                                log.LogInformation("Out: Candidate = " + candidateCIDR.ToString() + ", desired = " + desiredAddressSpace);
                                if ((null == desiredAddressSpace) || (candidateCIDR.ToString().Equals(desiredAddressSpace))) { 
                                    log.LogInformation("In: Candidate = " + candidateCIDR.ToString() + ", desired = " + desiredAddressSpace);
                                    if (null != desiredAddressSpace)
                                    {
                                        foundDesiredAddressSpace = true;
                                    }

                                    // Get a list of all CIDRs that could possibly fit into the given address space with the CIDR range requested
                                    IPNetworkCollection candidateSubnets = candidateCIDR.Subnet(cidr);
                                    IList usedSubnets = new ArrayList();

                                    // Get every Azure subnet in the VNet
                                    SubnetCollection usedSubnetsAzure = vNet.GetSubnets();

                                    // Convert into IPNetwork object list
                                    foreach (SubnetResource usedSubnet in usedSubnetsAzure)
                                    {
                                        usedSubnets.Add(IPNetwork2.Parse(usedSubnet.Data.AddressPrefix));
                                    }

                                    // Check each candidate subnet CIDR
                                    foreach (IPNetwork2 candidateSubnet in candidateSubnets)
                                    {
                                        if (!success)
                                        {
                                            foundBadSubnetInCandidateCIDR = false;
                                            // Go through each Azure subnet in VNet, check against candidate
                                            foreach (IPNetwork2 usedSubnet in usedSubnets)
                                            {
                                                if (!foundBadSubnetInCandidateCIDR && !(usedSubnet.Overlap(candidateSubnet)))
                                                {
                                                    success = true;
                                                    foundSubnet = candidateSubnet.ToString();
                                                    foundAddressSpace = candidateCIDR.ToString();
                                                }
                                                else
                                                {
                                                    foundBadSubnetInCandidateCIDR = true;
                                                }
                                            }

                                            if (foundBadSubnetInCandidateCIDR)
                                            {
                                                success = false;
                                                foundBadSubnetInCandidateCIDR = false;
                                                httpStatusCode = HttpStatusCode.NotFound;
                                            }
                                        }
                                    }
                                }
                            }

                            if (null != desiredAddressSpace && !foundDesiredAddressSpace)
                            {
                                httpStatusCode = HttpStatusCode.NotFound;
                                errorMessage = "Requested address space (" + desiredAddressSpace + ") not found in VNet " + resourceGroupName + "/" + virtualNetworkName;
                            }
                            else
                            {
                                if (!success)
                                {
                                    httpStatusCode = HttpStatusCode.NotFound;
                                    errorMessage = "VNet " + resourceGroupName + "/" + virtualNetworkName + " cannot accept a subnet of size " + cidr;
                                }
                            }
                        }
                    }
                    else
                    {
                        httpStatusCode = HttpStatusCode.BadRequest;
                        errorMessage = "Invalid CIDR size requested: " + cidrString;
                    }
                }
                else
                {
                    httpStatusCode = HttpStatusCode.BadRequest;
                    errorMessage = "Invalid input: " + errorMessage;
                }
            }
            catch (Exception e)
            {
                httpStatusCode = HttpStatusCode.InternalServerError;
                error = e;
                // empty code var will signal error
            }

            ObjectResult result = null;
            if ((null == errorMessage) && success)
            {
                proposedSubnetResponse.proposedCIDR = foundSubnet;
                proposedSubnetResponse.addressSpace = foundAddressSpace;

                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(proposedSubnetResponse, options);

                result = new OkObjectResult(jsonString);
                // result = new OkObjectResult("Available CIDR block for CIDR size " + cidr + " = " + foundSubnet);
            }
            else
            {
                if (null != error)
                {
                    errorMessage = error.Message;
                }

                var customError = new CustomError {
                    code = "" + ((int)httpStatusCode),
                    message = httpStatusCode.ToString() + ", " +  errorMessage
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(customError, options);

                result = new BadRequestObjectResult(jsonString);
            }

            return result;
        }

        private static string validateInput(string subscriptionId, string virtualNetworkName, string resourceGroupName, string cidrString, string desiredAddressSpace)
        {
            string errorMessage = null;

            if (null == subscriptionId)
            {
                errorMessage = "subscriptionId is null";
            }
            else if (null == virtualNetworkName)
            {
                errorMessage = "virtualNetworkName is null";
            }
            else if (null == resourceGroupName)
            {
                errorMessage = "resourceGroupName is null";
            }
            else if (null == cidrString)
            {
                errorMessage = "cidr is null";
            }
            else if (!validateCIDRBlock(desiredAddressSpace))
            {
                errorMessage = "desiredAddressSpace is invalid";
            }

            return errorMessage;
        }

        private static bool validateCIDRBlock(string inCIDRBlock)
        {
            bool isGood = false;

            if (null == inCIDRBlock)
            {
                isGood = true;
            } else
            {
                try
                {
                   // IPAddress.Parse(inCIDRBlock);
                    IPNetwork2.Parse(inCIDRBlock);
                    isGood = true;
                } catch (Exception ex)
                {
                    isGood = false;
                }
            }

            return isGood;
        }

        private static bool validateCIDR(string inCIDR)
        {
            bool isGood = false;

            try
            {
                byte cidr = Byte.Parse(inCIDR);
                isGood = (2 <= cidr && 29 >= cidr);
            }
            catch
            {

            }

            return isGood;
        }
    }
}