#!/usr/bin/env bash

function_app_name="fnc-QdMxg9"
code_directory="Find-NextCidrRange"
additional_stack_params="--csharp --force"

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

cd ${code_directory}

if [[ -n "$(az account show)" && "$(command -v func)" ]]; then
    func azure functionapp publish "${function_app_name}" ${additional_stack_params}
    print_success "Deployment complete" && exit 0

    else
      print_alert "Something went wrong"
      print_error "Please check any outputs and try again" && exit 1
fi
