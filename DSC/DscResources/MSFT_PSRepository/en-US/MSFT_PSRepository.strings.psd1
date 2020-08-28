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
    GetTargetResourceMessage  = Return the current state of the repository '{0}'.
    RepositoryNotFound        = The repository '{0}' was not found.
    TestTargetResourceMessage = Determining if the repository '{0}' is in the desired state.
    InDesiredState            = Repository '{0}' is in the desired state.
    NotInDesiredState         = Repository '{0}' is not in the desired state.
    RepositoryExist           = Updating the properties of the repository '{0}'.
    RepositoryDoesNotExist    = Creating the repository '{0}'.
    RemoveExistingRepository  = Removing the repository '{0}'.
'@
