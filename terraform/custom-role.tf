data "azurerm_role_definition" "network_contributor" {
  name = "Network Contributor"
}

module "roles" {
  source = "registry.terraform.io/libre-devops/custom-roles/azurerm"

  create_role = false
  assign_role = true

  roles = [
    {
      role_definition_id                    = data.azurerm_role_definition.network_contributor.role_definition_id
      role_assignment_name                  = "${replace(replace(title(module.fnc_app.fnc_app_name), "-", ""), " ", "")}${replace(data.azurerm_role_definition.network_contributor.name, " ", "")}Assignment" #FncAppNetworkContributorAssignment
      role_assignment_description           = "Role Assignment to assign function ${module.fnc_app.fnc_app_name} as ${data.azurerm_role_definition.network_contributor.name}"
      role_assignment_scope                 = data.azurerm_management_group.current_mgmt_group.id # Tenant root group
      role_assignment_assignee_principal_id = module.fnc_app.fnc_identity[0].principal_id
    },
  ]
}

