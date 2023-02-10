terraform {
  #Use the latest by default, uncomment below to pin or use hcl.lck
  required_providers {
    azurerm = {
      source = "hashicorp/azurerm"
    }
  }
  backend "azurerm" {
    storage_account_name = "saldoeuwdevmgt01"
    container_name       = "blobldoeuwdevmgt01"
    key                  = "lbdo-mi-fnc-app.terraform.tfstate"
  }
}