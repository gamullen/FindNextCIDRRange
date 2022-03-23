# FindNextCIDRRange
This Azure Function will find the next available CIDR range for the given virtual network and CIDR suffix/size.
The goal is to make programmatic subnet creation easier.

The format is:

`https://{{pathToFunctionApp}}?subscriptionId={{subscriptionId}}&resourceGroupName={{resourceGroupName}}&virtualNetworkName={{virtualNetworkName}}&cidr={{cidr}}`

