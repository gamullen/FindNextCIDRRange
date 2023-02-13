# Intro

Hello :wave:

This repo is a fork from the original creator [gamullen](https://github.com/gamullen/FindNextCIDRRange)

The fork aims to re-write the app in Python or atleast something similar to it, as well as integrate it with some CI/CD, terraform and other bits and bobs.

## What this Function App does

This repo hosts all the code and the mechanisms to deploy a Linux Azure Function App into the Libre DevOps tenant, with this function app acting as a method of getting the next CIDR range in a VNet.  Please check and the [original documentation and blog post](https://techcommunity.microsoft.com/t5/azure-networking-blog/programmatically-find-next-available-cidr-for-subnet/ba-p/3266016) for information on the original app. 

This repo:

- Build function app and needed resources using terraform
- Deploy code via GitHub Actions

The function itself is an HTTP function, which, will only run on request. Please note, the function app does require reader permissions over the virtual network.

**Worth a note, that this won't work out the box for you, you are expected to use your own intuition or reach out for help :smile:**

There are 1 functions:
- `Find-NextCidrRange` 

You can query the API by feeding it the parameters in the following format via a HTTP GET request:

`https://{{pathToFunctionApp}}?subscription_id={{subscriptionId}}&resource_group_name={{resourceGroupName}}&virtual_network_name={{virtualNetworkName}}&new_subnet_size={{cidr}}`

So, for example:

`https://fnc-ldo-euw-dev-01.azurewebsites.net/api/Find-NextCidrRange?subscription_id=aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee&resource_group_name=rg-ldo-euw-dev-build&virtual_network_name=vnet-ldo-euw-dev-01&new_subnet_size=24`

The result is given back as a suggested subnet in your range, for example:

```text
curl -X GET "https://fnc-ldo-euw-dev-01.azurewebsites.net/api/Find-NextCidrRange?subscription_id=aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee&resource_group_name=rg-ldo-euw-dev-build&virtual_network_name=vnet-ldo-euw-dev-01&new_subnet_size=24"
10.0.0.0/24
```

## Other Usages

If you would like to test the functionality of the script without deploying the app, check out the `debug/` folder, which is essentially the same code, but made with the intent of not using it inside a function app and querying directly.

```python
python3 debug/test.py
10.0.0.0/24
```

Later, you may find it useful to use it with terraform, you can do this using the HTTP resource:

```hcl

locals {
  subscription_id      = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
  resource_group_name  = "rg-ldo-euw-dev-build"
  virtual_network_name = "vnet-ldo-euw-dev-01"
  subnet_size          = 24
}

data "http" "next_subnet" {
  url = "https://fnc-ldo-euw-dev-01.azurewebsites.net/api/Find-NextCidrRange?subscription_id=${local.subscription_id}&resource_group_name=${local.resource_group_name}&virtual_network_name=${local.virtual_network_name}&new_subnet_size=${local.subnet_size}"
}

output "next_subnet" {
  value = data.http.next_subnet.response_body
}

_Please note, my function app will be taken down by Terraform regularly, chances are if you try to test query it, it will fail, so it is suggested you try your own_

## Building the environment

At the time of writing, this project only supports Azure DevOps continuous integration and is set up to deploy using some expected items in the Libre DevOps Azure DevOps instance.

You can freely use the Libre DevOps terraform modules used to deploy these resources as well as the pipeline templates, but setting up the bits in between will be up to you.  You can find this code in the `terraform/` folder, and will need the usual stuff.

### Terraform Build

The terraform build (should you wish to use it) deploys a number of resources as well as sets up permissions and some access keys.  At a high level it:

- Deploys 1x Resource Group
- Deploys 1x Linux App Service Plan on a Consumpution Plan Basis
- Deploys 1x Storage Account, hot access tier, with 1 blob container, a SAS key and stores the repos code as a ZIP file in that blob container
- Deploys 1x Log Analytics Workspace (for Application Insights, a shared ID is recomended for larger enterprises.)
- Deploys 1x Linux Function App with Web based application insights
- Deploys 1x Role Assignment to give  the SystemAssigned Managed Identity of the Function App access to the Tenant Root Group as Network Contributor (needed to list the subnets etc).  This can be assigned at a smaller scope should you configure it to do so.


## Known Bugs

### Deploying with anything greater than Python 3.9x

If you try to deploy the code using an interpeter newer than Python3.9, you will likely get some bugs from the package versions in the crypto library.  I am working on making this a docker container so I don't need to put up with it myself.