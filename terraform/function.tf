module "plan" {
  source = "registry.terraform.io/libre-devops/service-plan/azurerm"

  rg_name  = module.rg.rg_name
  location = module.rg.rg_location
  tags     = module.rg.rg_tags

  app_service_plan_name          = "asp-${var.short}-${var.loc}-${terraform.workspace}-01"
  add_to_app_service_environment = false

  os_type  = "Linux"
  sku_name = "Y1"
}

#checkov:skip=CKV2_AZURE_145:TLS 1.2 is allegedly the latest supported as per hashicorp docs
module "fnc_app" {
  source = "libre-devops/linux-function-app/azurerm"

  rg_name  = module.rg.rg_name
  location = module.rg.rg_location
  tags     = module.rg.rg_tags

  app_name        = "fnc-${var.short}-${var.loc}-${terraform.workspace}-01"
  service_plan_id = module.plan.service_plan_id

  connect_app_insights_to_law_workspace = true
  enable_app_insights                   = true
  workspace_id                          = module.law.law_id
  app_insights_name                     = "appi-${var.short}-${var.loc}-${terraform.workspace}-01"
  app_insights_type                     = "web"

  app_settings = {
    ARM_SUBSCRIPTION_ID      = data.azurerm_client_config.current_creds.subscription_id
    ARM_TENANT_ID            = data.azurerm_client_config.current_creds.tenant_id
    FUNCTION_APP_NAME        = "fnc-${var.short}-${var.loc}-${terraform.workspace}-01"
    RESOURCE_GROUP_NAME      = module.rg.rg_name
    WEBSITE_RUN_FROM_PACKAGE = "https://${module.sa.sa_name}.blob.core.windows.net/${azurerm_storage_container.storage_container_function.name}/${azurerm_storage_blob.storage_blob_function.name}${data.azurerm_storage_account_blob_container_sas.storage_account_blob_container_sas.sas}",
  }

  storage_account_name          = module.sa.sa_name
  storage_account_access_key    = module.sa.sa_primary_access_key
  storage_uses_managed_identity = "true"

  identity_type = "SystemAssigned"

  functions_extension_version = "~4"

  settings = {
    site_config = {
      minimum_tls_version = "1.2"
      http2_enabled       = true

      application_stack = {
        python_version = 3.9
      }
    }

    auth_settings = {
      enabled                       = false
      runtime_version               = "~1"
      unauthenticated_client_action = "AllowAnonymous"
    }
  }
}
