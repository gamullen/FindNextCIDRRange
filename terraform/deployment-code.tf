locals {
  # These may depend on the project so I have tried to template them out
  project_name             = "function"
  archive_file_type        = "zip"
  code_blob_container_name = "${local.project_name}releases"
  code_path                = "../src/Python"
  dependencies_path        = "${local.code_path}/requirements.txt"
  shell_interpreter        = ["pwsh", "-Command"]
  dependencies_install     = "pip3 install -r ${local.dependencies_path}"
  output_file_name         = "${local.project_name}.${local.archive_file_type}"
  output_path              = "${path.module}/${local.output_file_name}"
}

resource "azurerm_storage_container" "storage_container_function" {
  name                 = local.code_blob_container_name
  storage_account_name = module.sa.sa_name
}

resource "azurerm_storage_blob" "storage_blob_function" {
  name                   = "${local.project_name}-${substr(data.archive_file.code_zip.output_md5, 0, 6)}.${local.archive_file_type}"
  storage_account_name   = module.sa.sa_name
  storage_container_name = azurerm_storage_container.storage_container_function.name
  type                   = "Block"
  content_md5            = data.archive_file.code_zip.output_md5
  source                 = local.output_path
}

resource "null_resource" "dependencies_install" {
  triggers = {
    requirements_md5 = "${filemd5(local.dependencies_path)}"
  }
  provisioner "local-exec" {
    command     = local.dependencies_install
    interpreter = local.shell_interpreter
  }
}

data "archive_file" "code_zip" {
  type        = local.archive_file_type
  source_dir  = local.code_path
  output_path = local.output_path

  depends_on = [null_resource.dependencies_install]
}
