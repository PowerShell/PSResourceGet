# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe "Create PSCredentialInfo with VaultName and SecretName" -tags 'CI' {

    It "Verifies VaultName is not empty" {
        $randomSecret = [System.IO.Path]::GetRandomFileName()
        { New-Object Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo ("", $randomSecret) } | Should -Throw -ErrorId "ConstructorInvokedThrowException,Microsoft.PowerShell.Commands.NewObjectCommand"
    }

    It "Verifies SecretName is not empty" {
        { New-Object Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo ("testvault", "") } | Should -Throw -ErrorId "ConstructorInvokedThrowException,Microsoft.PowerShell.Commands.NewObjectCommand"
    }

    It "Creates PSCredentialInfo successfully if VaultName and SecretName are non-empty" {
        $randomSecret = [System.IO.Path]::GetRandomFileName()
        $credentialInfo = New-Object Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo ("testvault", $randomSecret)
        $credentialInfo.VaultName | Should -Be "testvault"
        $credentialInfo.SecretName | Should -Be $randomSecret
    }
}

Describe "Create PSCredentialInfo with VaultName, SecretName, and Credential" -tags 'CI' {

    It "Creates PSCredentialInfo successfully if Credential is null" {
        $randomSecret = [System.IO.Path]::GetRandomFileName()
        $credentialInfo = New-Object Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo ("testvault", $randomSecret)
        
        $credentialInfo.VaultName | Should -Be "testvault"
        $credentialInfo.SecretName | Should -Be $randomSecret
    }

    It "Creates PSCredentialInfo successfully if Credential is non-null and of type PSCredential" {
        $randomSecret = [System.IO.Path]::GetRandomFileName()
        $randomPassword = [System.IO.Path]::GetRandomFileName()
        $credential = New-Object System.Management.Automation.PSCredential ("username", (ConvertTo-SecureString $randomPassword -AsPlainText -Force))
        $credentialInfo = New-Object Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo ("testvault", $randomSecret, $credential)

        $credentialInfo.VaultName | Should -Be "testvault"
        $credentialInfo.SecretName | Should -Be $randomSecret
    }
}
