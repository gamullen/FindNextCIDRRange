terraform {
  required_version = "1.2.4"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "> 3.1.0"
    }
  }
  backend "local" {
    path = "." # It is recomended to use a Azure Storage Account, S3 bucket or similar for state storage
  }
}