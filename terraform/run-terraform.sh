#!/usr/bin/env bash

vnet_rg_name="rg-ldo-euw-dev-build"  # The resource group name where your _existing_ VNet is.
vnet_name="vnet-ldo-euw-dev-01"      # The name of your existing virtual network.

rg_name="cidr-app"                # The name of the resource group which terraform will create and add the function app to
resource_location="westeurope"    # The name of the Azure region all terraform (function app etc) resources will be created with


set -eou pipefail

print_success() {
	lightcyan='\033[1;36m'
	nocolor='\033[0m'
	echo -e "${lightcyan}$1${nocolor}"
}

print_error() {
	lightred='\033[1;31m'
	nocolor='\033[0m'
	echo -e "${lightred}$1${nocolor}"
}

print_alert() {
	yellow='\033[1;33m'
	nocolor='\033[0m'
	echo -e "${yellow}$1${nocolor}"
}

#Checks if Azure-CLI is installed
if [ "$(command -v az)" ] && [ "$(command -v jq)" ] && [ "$(command -v unzip)" ] && [ "$(command -v curl)" ]; then
	print_success "Azure-CLI, curl, unzip and jq are installed!, attempting az login" && sleep 2s && az login --output none

else
	print_error "You must install Azure CLI, curl, unzip and jq installed to use this script to use this script" && exit 1

fi

export TF_VAR_vnet_rg_name=${vnet_rg_name}
export TF_VAR_vnet_name=${vnet_name}
export TF_VAR_rg_name=${rg_name}
export TF_VAR_resource_location=${resource_location}

terraformLatestVersion=$(curl -sL https://releases.hashicorp.com/terraform/index.json | jq -r '.versions[].builds[].url' | egrep 'rc|beta|alpha' | egrep 'linux.*amd64' | tail -1) &&
	curl "${terraformLatestVersion}" -o terraformtemp.zip &&
	unzip terraformtemp.zip && rm -rf terraform.zip

if ./terraform init; then

	print_alert "Running terraform plan & apply now" &&
		./terraform validate &&
		./terraform plan -out pipeline.plan &&
		sleep 3s &&
		./terraform apply -auto-approve pipeline.plan

  rm -rf .terraform*
  rm -rf terraform
  rm -rf terraform*.zip
	rm -rf pipeline.plan
	rm -rf .run-terraform.sh.swp
	unset TF_VAR_vnet_rg_name
	unset TF_VAR_vnet_name
	unset TF_VAR_rg_name
	unset TF_VAR_resource_location
	exit 0

else
	print_error "The terraform has failed to start or exited during runtime" &&
  rm -rf .terraform*
  rm -rf terraform
  rm -rf terraform*.zip
	rm -rf pipeline.plan
	rm -rf .run-terraform.sh.swp
	unset TF_VAR_vnet_rg_name
	unset TF_VAR_vnet_name
	unset TF_VAR_rg_name
	unset TF_VAR_resource_location
	exit 1
fi
