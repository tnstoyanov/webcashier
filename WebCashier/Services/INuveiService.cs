using System.Threading.Tasks;
namespace WebCashier.Services
{
    public record NuveiRequest(decimal Amount, string Currency, string UserTokenId, string ItemName);
    public record NuveiFormField(string Key, string Value);
    public record NuveiFormResponse(string SubmitFormUrl, IReadOnlyList<NuveiFormField> Fields, string Method = "post");

    public interface INuveiService
    {
        NuveiFormResponse BuildPaymentForm(NuveiRequest request, string baseUrl);
    }
}
