# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe "Create PSCredentialInfo with VaultName and SecretName" -tags 'CI' {

    It "Verifies VaultName is not empty" {
        { New-Object Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo ("", "testsecret") } | Should -Throw -ErrorId "ConstructorInvokedThrowException,Microsoft.PowerShell.Commands.NewObjectCommand"
    }

    It "Verifies SecretName is not empty" {
        { New-Object Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo ("testvault", "") } | Should -Throw -ErrorId "ConstructorInvokedThrowException,Microsoft.PowerShell.Commands.NewObjectCommand"
    }

    It "Creates PSCredentialInfo successfully if VaultName and SecretName are non-empty" {
        $credentialInfo = New-Object Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo ("testvault", "testsecret")
        $credentialInfo.VaultName | Should -Be "testvault"
        $credentialInfo.SecretName | Should -Be "testsecret"
    }
}

Describe "Create PSCredentialInfo with VaultName, SecretName, and Credential" -tags 'CI' {

    It "Creates PSCredentialInfo successfully if Credential is null" {
        $credentialInfo = New-Object Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo ("testvault", "testsecret", $null)
        
        $credentialInfo.VaultName | Should -Be "testvault"
        $credentialInfo.SecretName | Should -Be "testsecret"
    }

    It "Creates PSCredentialInfo successfully if Credential is non-null and of type PSCredential" {
        $credential = New-Object System.Management.Automation.PSCredential ("username", (ConvertTo-SecureString "password" -AsPlainText -Force))
        $credentialInfo = New-Object Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo ("testvault", "testsecret", $credential)

        $credentialInfo.VaultName | Should -Be "testvault"
        $credentialInfo.SecretName | Should -Be "testsecret"
    }
}

Describe "Create PSCredentialInfo from a PSObject" -tags 'CI' {

    It "Throws if VaultName is null" {
        $customObject = New-Object PSObject
        { New-Object Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo $customObject } | Should -Throw -ErrorId "ConstructorInvokedThrowException,Microsoft.PowerShell.Commands.NewObjectCommand"
    }

    It "Throws if SecretName is null" {
        $customObject = New-Object PSObject
        $customObject | Add-Member -Name "VaultName" -Value "testvault" -MemberType NoteProperty
        { New-Object Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo $customObject } | Should -Throw -ErrorId "ConstructorInvokedThrowException,Microsoft.PowerShell.Commands.NewObjectCommand"
    }

    It "Creates PSCredentialInfo successfully from PSObject with VaultName and SecretName" {
        $properties = [PSCustomObject]@{
            VaultName = "testvault"
            SecretName = "testsecret"
        }

        $credentialInfo = [Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo] $properties

        $credentialInfo.VaultName | Should -Be "testvault"
        $credentialInfo.SecretName | Should -Be "testsecret"
    }

    It "Creates PSCredentialInfo successfully from PSObject with VaultName, SecretName and Credential" {
        $credential = New-Object System.Management.Automation.PSCredential ("username", (ConvertTo-SecureString "password" -AsPlainText -Force))
        $properties = [PSCustomObject]@{
            VaultName = "testvault"
            SecretName = "testsecret"
            Credential = [PSCredential] $credential
        }

        $credentialInfo = [Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo] $properties

        $credentialInfo.VaultName | Should -Be "testvault"
        $credentialInfo.SecretName | Should -Be "testsecret"
        $credentialInfo.Credential.UserName | Should -Be "username"
        $credentialInfo.Credential.GetNetworkCredential().Password | Should -Be "password"
        
    }
}
