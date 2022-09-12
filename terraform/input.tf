variable "rg_name" {
  type    = string
  default = "rg"
  description = "The resource group name"
}

variable "location" {
  type = string
  default = "westeurope"
  description = "The name of the Azure region the resources to be deployed to"
}

variable "vnet_rg_name" {
  type = string
  description = "The resource group name, that the VNet you want the function app to be able to provide integration with, sits in"
}


variable "vnet_name" {
  type = string
  description = "The name of the VNet you want the function app to monitor"
}
