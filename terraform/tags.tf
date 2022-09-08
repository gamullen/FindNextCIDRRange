locals {
  tags = {
    "LastUpdated" = formatdate("hh:mm:DD-MM-YYYY", timestamp())
  }
}
