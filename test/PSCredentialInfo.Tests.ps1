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

    It "Creates PSCredentialInfo successfully from hashtable" {
        $randomSecret = [System.IO.Path]::GetRandomFileName()
        $randomPassword = [System.IO.Path]::GetRandomFileName()
        $credential = New-Object System.Management.Automation.PSCredential ("username", (ConvertTo-SecureString $randomPassword -AsPlainText -Force))
        $hash = @{ "VaultName" = "testvault" ; "SecretName" = $randomSecret ; "Credential" = $credential }
        $credentialInfo = New-Object Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo ($hash)
        
        $credentialInfo.VaultName | Should -Be "testvault"
        $credentialInfo.SecretName | Should -Be $randomSecret
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
        $randomSecret = [System.IO.Path]::GetRandomFileName()
        $properties = [PSCustomObject]@{
            VaultName = "testvault"
            SecretName = $randomSecret
        }

        $credentialInfo = [Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo] $properties

        $credentialInfo.VaultName | Should -Be "testvault"
        $credentialInfo.SecretName | Should -Be $randomSecret
    }

    It "Creates PSCredentialInfo successfully from PSObject with VaultName, SecretName and PSCredential Credential" {
        $randomSecret = [System.IO.Path]::GetRandomFileName()
        $randomPassword = [System.IO.Path]::GetRandomFileName()

        $credential = New-Object System.Management.Automation.PSCredential ("username", (ConvertTo-SecureString $randomPassword -AsPlainText -Force))
        $properties = [PSCustomObject]@{
            VaultName = "testvault"
            SecretName = $randomSecret
            Credential = [PSCredential] $credential
        }

        $credentialInfo = [Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo] $properties

        $credentialInfo.VaultName | Should -Be "testvault"
        $credentialInfo.SecretName | Should -Be $randomSecret
        $credentialInfo.Credential.UserName | Should -Be "username"
        $credentialInfo.Credential.GetNetworkCredential().Password | Should -Be $randomPassword
    }
 
    It "Creates PSCredentialInfo successfully from PSObject with VaultName, SecretName and string Credential" {
        $randomSecret = [System.IO.Path]::GetRandomFileName()
        $randomPassword = [System.IO.Path]::GetRandomFileName()

        $properties = [PSCustomObject]@{
            VaultName = "testvault"
            SecretName = $randomSecret
            Credential = $randomPassword
        }

        $credentialInfo = [Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo] $properties

        $credentialInfo.VaultName | Should -Be "testvault"
        $credentialInfo.SecretName | Should -Be $randomSecret
        $credentialInfo.Credential.GetNetworkCredential().Password | Should -Be $randomPassword
    }

    It "Creates PSCredentialInfo successfully from PSObject with VaultName, SecretName and SecureString Credential" {
        $randomSecret = [System.IO.Path]::GetRandomFileName()
        $randomPassword = [System.IO.Path]::GetRandomFileName()

        $secureString = ConvertTo-SecureString $randomPassword -AsPlainText -Force
        $properties = [PSCustomObject]@{
            VaultName = "testvault"
            SecretName = $randomSecret
            Credential = $secureString
        }

        $credentialInfo = [Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo] $properties

        $credentialInfo.VaultName | Should -Be "testvault"
        $credentialInfo.SecretName | Should -Be $randomSecret
        $credentialInfo.Credential.GetNetworkCredential().Password | Should -Be $randomPassword
    }
}
