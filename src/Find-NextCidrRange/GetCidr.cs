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

using Azure;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Resources;
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

        [FunctionName("GetCidr")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string subscriptionId = req.Query["subscriptionId"];
            string virtualNetworkName = req.Query["virtualNetworkName"];
            string resourceGroupName = req.Query["resourceGroupName"];
            string cidrString = req.Query["cidr"];
            string desiredAddressSpace = req.Query["addressSpace"];
            Exception error = null;
            string errorMessage = null;
            bool success = false;
            string foundSubnet = null;
            string foundAddressSpace = null;
            byte cidr;
            VirtualNetworkResource vNet = null;

            try
            {
                // Validate the input params
                errorMessage = ValidateInput(subscriptionId, virtualNetworkName, resourceGroupName, cidrString, desiredAddressSpace);
                if (null == errorMessage)
                {
                    // Make sure the CIDR is valid
                    if (ValidateCIDR(cidrString))
                    {
                        cidr = Byte.Parse(cidrString);

                        // Get a client for the SDK calls
                        var armClient = new ArmClient(new DefaultAzureCredential(), subscriptionId);

                        var subscription = await armClient.GetDefaultSubscriptionAsync();
                        ResourceGroupResource rg = await subscription.GetResourceGroupAsync(resourceGroupName);

                        vNet = await rg.GetVirtualNetworkAsync(virtualNetworkName);

                        var vNetCIDRs = new HashSet<IPNetwork2>();

                        foreach (string ip in vNet.Data.AddressPrefixes)
                        {
                            IPNetwork2 vNetCIDR = IPNetwork2.Parse(ip);
                            if (cidr >= vNetCIDR.Cidr && (null == desiredAddressSpace || vNetCIDR.ToString().Equals(desiredAddressSpace)))
                            {
                                log.LogInformation("In: Candidate = " + vNetCIDR.ToString() + ", desired = " + desiredAddressSpace);
                                foundSubnet = GetValidSubnetIfExists(vNet, vNetCIDR, cidr);
                                foundAddressSpace = vNetCIDR.ToString();

                                if (null != foundSubnet)
                                {
                                    log.LogInformation("Valid subnet is found: " + foundSubnet);
                                    success = true;
                                    break;
                                }
                            }
                        }

                        if (!success)
                        {
                            httpStatusCode = HttpStatusCode.NotFound;
                            if (null == desiredAddressSpace)
                                errorMessage = "VNet " + resourceGroupName + "/" + virtualNetworkName + " cannot accept a subnet of size " + cidr;
                            else
                                errorMessage = "Requested address space (" + desiredAddressSpace + ") not found in VNet " + resourceGroupName + "/" + virtualNetworkName;
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

            catch (RequestFailedException ex) when (ex.Status == 404) // case the resource group or vnet doesn't exist
            {
                httpStatusCode = HttpStatusCode.NotFound;
                error = ex;
            }
            catch (Exception e)
            {

                httpStatusCode = HttpStatusCode.InternalServerError;
                error = e;
                // empty code var will signal error
            }

            ObjectResult result;
            if (null == errorMessage && success)
            {
                ProposedSubnetResponse proposedSubnetResponse = new ProposedSubnetResponse()
                {
                    name = virtualNetworkName,
                    id = vNet.Id,
                    type = vNet.Id.ResourceType,
                    location = vNet.Data.Location,
                    proposedCIDR = foundSubnet,
                    addressSpace = foundAddressSpace

                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(proposedSubnetResponse, options);

                result = new OkObjectResult(jsonString);
            }
            else
            {   if(null != error) 
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

        private static string ValidateInput(string subscriptionId, string virtualNetworkName, string resourceGroupName, string cidrString, string desiredAddressSpace)
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
            else if (!ValidateCIDRBlock(desiredAddressSpace))
            {
                errorMessage = "desiredAddressSpace is invalid";
            }

            return errorMessage;
        }

        private static bool ValidateCIDRBlock(string inCIDRBlock)
        {
            bool isGood = false;

            if (null == inCIDRBlock)
            {
                isGood = true;
            }
            else
            {
                try
                {
                   // IPAddress.Parse(inCIDRBlock);
                    IPNetwork2.Parse(inCIDRBlock);
                    isGood = true;
                } catch 
                {
                    isGood = false;
                }
            }

            return isGood;
        }

        private static bool ValidateCIDR(string inCIDR)
        {
            bool isGood = false;

            byte cidr;

            if(Byte.TryParse(inCIDR, out cidr))
            { 
                isGood = (2 <= cidr && 29 >= cidr);
            }

            return isGood;
        }

        private static string GetValidSubnetIfExists(VirtualNetworkResource vNet, IPNetwork2 vNetCIDR, Byte cidr)
        {
            var usedSubnets = new List<IPNetwork2>();

            // Get every Azure subnet in the VNet
            SubnetCollection usedSubnetsAzure = vNet.GetSubnets();

            // Get a list of all CIDRs that could possibly fit into the given address space with the CIDR range requested
            IPNetworkCollection candidateSubnets = vNetCIDR.Subnet(cidr);

            // Convert into IPNetwork object list
            foreach (SubnetResource usedSubnet in usedSubnetsAzure)
            {
                var prefixes = new List<string>();
                
                prefixes.AddRange(usedSubnet.Data.AddressPrefixes);

                if ( null != usedSubnet.Data.AddressPrefix && !prefixes.Contains(usedSubnet.Data.AddressPrefix))
                    prefixes.Add(usedSubnet.Data.AddressPrefix);

                foreach(var prefix in usedSubnet.Data.AddressPrefixes)
                    usedSubnets.Add(IPNetwork2.Parse(prefix));
            }

            foreach (IPNetwork2 candidateSubnet in candidateSubnets)
            {
                bool subnetIsValid = true;
                // Go through each Azure subnet in VNet, check against candidate
                foreach (IPNetwork2 usedSubnet in usedSubnets)
                {
                    if (usedSubnet.Overlap(candidateSubnet))
                    {
                        subnetIsValid = false;
                        break; // stop the loop as the candidate is not valid (overlapping with existing subnets)
                    }
                }
                if (subnetIsValid)
                {
                    return candidateSubnet.ToString();
                }
            }
            // no valid subnet found
            return null;
        }

    }
}