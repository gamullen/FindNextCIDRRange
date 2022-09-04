# Intro

Hello :wave:

This repo is a fork from the original creator [gamullen](https://github.com/gamullen/FindNextCIDRRange)

The fork aims to add some functionality to the app, as well as integrate it with some CI/CD, terraform and other bits and bobs.

## What this Function App does

This repo hosts all the code and the mechanisms to deploy a Linux Azure Function App into the Libre DevOps tenant, with this function app acting as a method of getting the next CIDR range in a VNet.  Please check and the [original documentation and blog post](https://techcommunity.microsoft.com/t5/azure-networking-blog/programmatically-find-next-available-cidr-for-subnet/ba-p/3266016) for information on the original app. 

This repo:

- Build function app and needed resources using terraform
- Deploy code via GitHub Actions

The function itself is a HTTP function, which, will only run on request. Please note, the function app does require reader permissions over the virtual network.

There are 1 functions:
- `Get-Cidir` 

You can query the API by feeding it the parameters in the following format via a HTTP GET request:

`https://{{pathToFunctionApp}}?subscriptionId={{subscriptionId}}&resourceGroupName={{resourceGroupName}}&virtualNetworkName={{virtualNetworkName}}&cidr={{cidr}}`

So, for example:

`https://fnc-ldo-euw-dev-01.azurewebsites.net/api/getcidr?subscriptionId=09d383ee-8ed0-4374-ad9f-3344cabc323b&resourceGroupName=rg-ldo-euw-dev-build&virtualNetworkName=vnet-ldo-euw-dev-01&cidr=26`

With example output:

```json
{
  "name": "vnet-ldo-euw-dev-01",
  "id": "/subscriptions/09d383ee-8ed0-4374-ad9f-3344cabc323b/resourceGroups/rg-ldo-euw-dev-build/providers/Microsoft.Network/virtualNetworks/vnet-ldo-euw-dev-01",
  "type": "Microsoft.Network/virtualNetworks",
  "location": "westeurope",
  "proposedCIDR": "10.0.0.0/26"
}
```

## Building the environment

At the time of writing, this project only supports Azure DevOps continuous integration and is setup to deploy using some expected items in the Libre DevOps Azure DevOps instance.

You can freely use the modules used to deploy these resources as well as the pipeline templates, but setting up the bits in between will be up to you.

### Terraform Build
- 1x Resource Group
- 1x Linux Function app on Consumption Service Plan with Dotnet 6.0 Application Stack (up to date with the v3 Azurerm provider changes in terraform)
- 1x Storage Account, Hot access tier
- 1x Blob container with blob (anonymous access) for the URLs

