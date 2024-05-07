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
using System.Linq;

namespace FindNextCIDR {
    public static class GetCidr {
        public class ProposedSubnetResponse {
            public required string Name { get; set; }
            public required string ID { get; set; }
            public required string Type { get; set; }
            public required string Location { get; set; }
            public required string AddressSpace { get; set; }
            public required string ProposedCIDR { get; set; }
        }
        public class CustomError {
            public required string Code { get; set; }
            public required string Message { get; set; }
        }

        [FunctionName("GetCidr")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log) {
            log.LogInformation("C# HTTP trigger function processed a request.");

            // Check for valid input
            string[] requiredParameters = { "subscriptionId", "virtualNetworkName", "resourceGroupName", "cidr" };
            string missingParameter = requiredParameters.FirstOrDefault(parameter => string.IsNullOrWhiteSpace(req.Query[parameter]));
            if(missingParameter != null) return ResultError($"{missingParameter} is null or empty", HttpStatusCode.BadRequest);

            // Get the query parameters
            string subscriptionId = req.Query["subscriptionId"];
            string vnetName = req.Query["virtualNetworkName"];
            string rgName = req.Query["resourceGroupName"];
            string cidrString = req.Query["cidr"];
            string desiredAddressSpace = req.Query["addressSpace"];

            // Validate the CIDR block and CIDR size
            if (!ValidateCIDR(cidrString)) return ResultError("Invalid CIDR size requested: " + cidrString);
            if (!ValidateCIDRBlock(desiredAddressSpace)) return ResultError("desiredAddressSpace is invalid");

            ResourceGroupResource rg;
            VirtualNetworkResource vNet;
            try {
                var armClient = new ArmClient(new DefaultAzureCredential(), subscriptionId);
                var subscription = await armClient.GetDefaultSubscriptionAsync();
                rg = await subscription.GetResourceGroupAsync(rgName);
                vNet = await rg.GetVirtualNetworkAsync(vnetName);
            }
            catch (RequestFailedException ex) when (ex.Status == 404) {
                // case the resource group or vnet doesn't exist
                return ResultError(ex.ToString(), HttpStatusCode.NotFound);
            }
            catch (Exception e) {
                // empty code var will signal error
                return ResultError(e.ToString(), HttpStatusCode.InternalServerError);
            }

            byte cidr = Byte.Parse(cidrString);

            var matchingPrefixes = vNet.Data.AddressPrefixes
                .Select(prefix => IPNetwork2.Parse(prefix))
                .Where(vNetCIDR => cidr >= vNetCIDR.Cidr && (desiredAddressSpace == null || vNetCIDR.ToString().Equals(desiredAddressSpace)));

            foreach (var vNetCIDR in matchingPrefixes) {
                string foundSubnet = GetValidSubnetIfExists(vNet, vNetCIDR, cidr);
                string foundAddressSpace = vNetCIDR.ToString();

                if (foundSubnet != null) return ResultSuccess(vNet, vnetName, foundSubnet, foundAddressSpace);
            }

            string errMsg = desiredAddressSpace == null 
                ? "VNet " + rgName + "/" + vnetName + " cannot accept a subnet of size " + cidr 
                : "Requested address space (" + desiredAddressSpace + ") not found in VNet " + rgName + "/" + vnetName;

            return ResultError(errMsg, HttpStatusCode.NotFound);
        }
        private static BadRequestObjectResult ResultError(string errorMessage, HttpStatusCode httpStatusCode = HttpStatusCode.BadRequest) {
            var customError = new CustomError {
                Code = "" + ((int)httpStatusCode),
                Message = httpStatusCode.ToString() + ", " +  errorMessage
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(customError, options);

            return new BadRequestObjectResult(jsonString);;
        }
        private static OkObjectResult ResultSuccess(VirtualNetworkResource vNet, string virtualNetworkName, string foundSubnet, string foundAddressSpace) {
            ProposedSubnetResponse proposedSubnetResponse = new ProposedSubnetResponse() {
                Name = virtualNetworkName,
                ID = vNet.Id,
                Type = vNet.Id.ResourceType,
                Location = vNet.Data.Location,
                ProposedCIDR = foundSubnet,
                AddressSpace = foundAddressSpace
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(proposedSubnetResponse, options);

            return new OkObjectResult(jsonString);
        }

        private static bool ValidateCIDRBlock(string inCIDRBlock) {
            if (inCIDRBlock == null) return true; // no address space specified (ok as optional)

            try { IPNetwork2.Parse(inCIDRBlock); }
            catch { return false; }

            return true;
        }

        private static bool ValidateCIDR(string inCIDR) {
            if (Byte.TryParse(inCIDR, out global::System.Byte cidr)) return 2 <= cidr && 29 >= cidr;
            else return false;
        }

        private static string GetValidSubnetIfExists(VirtualNetworkResource vNet, IPNetwork2 requestedCIDR, byte cidr) {
            List<IPNetwork2> subnets = vNet.GetSubnets().Select(subnet => IPNetwork2.Parse(subnet.Data.AddressPrefix)).ToList();

            // Iterate through each candidate subnet
            foreach (IPNetwork2 candidateSubnet in requestedCIDR.Subnet(cidr)) {
                // Check if the candidate subnet overlaps with any existing subnet
                if (!subnets.Any(subnet => subnet.Overlap(candidateSubnet))) {
                    return candidateSubnet.ToString(); // Found a valid subnet, return it
                }
            }
            return null; // No valid subnet found
        }
    }
}
