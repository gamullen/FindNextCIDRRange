module "rg" {
  source = "registry.terraform.io/libre-devops/rg/azurerm"

  rg_name  = var.rg_name
  location = var.location
  tags     = local.tags

  #  lock_level = "CanNotDelete" // Do not set this value to skip lock
}