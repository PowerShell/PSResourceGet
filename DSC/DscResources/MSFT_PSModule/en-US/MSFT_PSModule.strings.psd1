#
# Copyright (c) Microsoft Corporation.
#
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
# IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
# FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
# AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
# LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
# OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
# THE SOFTWARE.
#
# culture = "en-US"
ConvertFrom-StringData -StringData @'
    FailToUninstall                 = Failed to uninstall the module '{0}'.
    FailToInstall                   = Failed to install the module '{0}'.
    InDesiredState                  = Resource '{0}' is in the desired state.
    NotInDesiredState               = Resource '{0}' is not in the desired state.
    ModuleFound                     = Module '{0}' is found on the node.
    ModuleNotFound                  = Module '{0}' is not found on the node.
    ModuleWithRightPropertyNotFound = Module '{0}' with the right version or other properties not found in the node.
    ModuleNotFoundInRepository      = Module '{0}' with the right version or other properties not found in the repository.
    StartGetModule                  = Begin invoking Get-Module '{0}'.
    StartFindModule                 = Begin invoking Find-PSResource '{0}'.
    StartInstallModule              = Begin invoking Install-PSResource '{0}' version '{1}' from '{2}' repository.
    StartUnInstallModule            = Begin invoking Remove-Item to remove the module '{0}' from the file system.
    InstalledSuccess                = Successfully installed the module '{0}'
    UnInstalledSuccess              = Successfully uninstalled the module '{0}'
    VersionMismatch                 = The installed module '{0}' has the version: '{1}'
    RepositoryMismatch              = The installed module '{0}' is from the '{1}' repository.
    FoundModulePath                 = Found the module path: '{0}'.
    InstallationPolicyWarning       = The module '{0}' was installed from the untrusted repository' {1}'. The InstallationPolicy is set to '{2}' to override the repository installation policy. If you trust the repository, set the repository installation policy to 'Trusted', that will also remove this warning.
    InstallationPolicyFailed        = The current installation policy do not allow installation from this repository. Your current installation policy is '{0}' and the repository installation policy is '{1}'. If you trust the repository, either change the repository installation policy, or set the parameter InstallationPolicy to 'Trusted' to override the repository installation policy.
    GetTargetResourceMessage        = Getting the current state of the module '{0}'.
    TestTargetResourceMessage       = Determining if the module '{0}' is in the desired state.
'@
