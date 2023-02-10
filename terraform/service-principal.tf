data "azuread_client_config" "current" {}

resource "azuread_application" "fnc_svp" {
  display_name = "svp-${var.short}-${var.loc}-${terraform.workspace}-expiryfnc-01"
  owners       = [data.azuread_client_config.current.object_id]
}

resource "azuread_service_principal" "fnc_svp" {
  application_id               = azuread_application.fnc_svp.application_id
  app_role_assignment_required = false
  owners                       = [data.azuread_client_config.current.object_id]
}

#resource "azuread_service_principal_delegated_permission_grant" "fnc_svp" {
#  service_principal_object_id          = azuread_service_principal.fnc_svp.object_id
#  resource_service_principal_object_id = data.azuread_client_config.current.object_id
#  claim_values                         = ["Application", "Application.ReadWrite.All"]
#}