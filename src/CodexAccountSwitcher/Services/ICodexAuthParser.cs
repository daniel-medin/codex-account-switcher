using CodexAccountSwitcher.Models;

namespace CodexAccountSwitcher.Services;

public interface ICodexAuthParser
{
    CodexAuthInfo Parse(byte[] authState);
    bool Matches(CodexAccount account, CodexAuthInfo authInfo);
    void ApplyIdentity(CodexAccount account, CodexAuthInfo authInfo);
}
