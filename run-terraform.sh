#!/usr/bin/env bash

set -xeou pipefail

vnet_rg_name="example-vnet-rg" # The resource group name your existing VNet exists on
vnet_name="vnet-example" # The name of your existing virtual network

rg_name="example-rg-name" # The name of the resource group which terraform will create and add the function app to
resource_location="uksouth" # The name of the Azure region all resources will be created with

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
if [[ ! $(command -v az) && $(command -v jq) && $(command -v unzip) && $(command -v curl)  ]] ;

then
    print_error "You must install Azure CLI, curl, unzip and jq installed to use this script to use this script" && exit 1

else
    print_success "Azure-CLI, curl, unzip and jq are installed!, attempting az login" && sleep 2s && az login

fi



terraformLatestVersion=$(curl -sL https://releases.hashicorp.com/terraform/index.json | jq -r '.versions[].builds[].url' | grep -E 'rc|beta|alpha' | grep -E 'linux.*amd64'  | tail -1) && \
    curl "${terraformLatestVersion}" -o terraformtemp.zip  && \
    unzip terraformtemp.zip && rm -rf terraform.zip

cd terraform && \
terraform init

rm -rf terraform