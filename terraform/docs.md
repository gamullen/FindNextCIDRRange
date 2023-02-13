## Requirements

No requirements.

## Providers

| Name | Version |
|------|---------|
| <a name="provider_archive"></a> [archive](#provider\_archive) | 2.3.0 |
| <a name="provider_azurerm"></a> [azurerm](#provider\_azurerm) | 3.43.0 |
| <a name="provider_null"></a> [null](#provider\_null) | 3.2.1 |

## Modules

| Name | Source | Version |
|------|--------|---------|
| <a name="module_fnc_app"></a> [fnc\_app](#module\_fnc\_app) | libre-devops/linux-function-app/azurerm | n/a |
| <a name="module_law"></a> [law](#module\_law) | registry.terraform.io/libre-devops/log-analytics-workspace/azurerm | n/a |
| <a name="module_network"></a> [network](#module\_network) | registry.terraform.io/libre-devops/network/azurerm | n/a |
| <a name="module_nsg"></a> [nsg](#module\_nsg) | registry.terraform.io/libre-devops/nsg/azurerm | n/a |
| <a name="module_plan"></a> [plan](#module\_plan) | registry.terraform.io/libre-devops/service-plan/azurerm | n/a |
| <a name="module_rg"></a> [rg](#module\_rg) | registry.terraform.io/libre-devops/rg/azurerm | n/a |
| <a name="module_roles"></a> [roles](#module\_roles) | registry.terraform.io/libre-devops/custom-roles/azurerm | n/a |
| <a name="module_sa"></a> [sa](#module\_sa) | registry.terraform.io/libre-devops/storage-account/azurerm | n/a |

## Resources

| Name | Type |
|------|------|
| [azurerm_storage_blob.storage_blob_function](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/storage_blob) | resource |
| [azurerm_storage_container.storage_container_function](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/storage_container) | resource |
| [null_resource.dependencies_install](https://registry.terraform.io/providers/hashicorp/null/latest/docs/resources/resource) | resource |
| [archive_file.code_zip](https://registry.terraform.io/providers/hashicorp/archive/latest/docs/data-sources/file) | data source |
| [azurerm_client_config.current_creds](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/data-sources/client_config) | data source |
| [azurerm_management_group.current_mgmt_group](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/data-sources/management_group) | data source |
| [azurerm_role_definition.network_contributor](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/data-sources/role_definition) | data source |
| [azurerm_storage_account_blob_container_sas.storage_account_blob_container_sas](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/data-sources/storage_account_blob_container_sas) | data source |

## Inputs

| Name | Description | Type | Default | Required |
|------|-------------|------|---------|:--------:|
| <a name="input_Regions"></a> [Regions](#input\_Regions) | Converts shorthand name to longhand name via lookup on map list | `map(string)` | <pre>{<br>  "eus": "East US",<br>  "euw": "West Europe",<br>  "uks": "UK South",<br>  "ukw": "UK West"<br>}</pre> | no |
| <a name="input_env"></a> [env](#input\_env) | This is passed as an environment variable, it is for the shorthand environment tag for resource.  For example, production = prod | `string` | `"dev"` | no |
| <a name="input_loc"></a> [loc](#input\_loc) | The shorthand name of the Azure location, for example, for UK South, use uks.  For UK West, use ukw. Normally passed as TF\_VAR in pipeline | `string` | `"euw"` | no |
| <a name="input_short"></a> [short](#input\_short) | This is passed as an environment variable, it is for a shorthand name for the environment, for example hello-world = hw | `string` | `"ldo"` | no |

## Outputs

No outputs.
