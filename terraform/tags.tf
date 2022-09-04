locals {
  tags = {
    Environment = "${upper(terraform.workspace)}"
    ProjectName = "${upper(var.short)}"
    CostCentre  = "${title("67/1888")}"
  }
}
