using api.Model.viaCep;

namespace api.Model.ViaCep
{
    public class ViaCepService
    {
        private readonly HttpClient _httpClient;

        public ViaCepService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ViaCepResponse?> BuscarPorCep(string cep)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<ViaCepResponse>($"https://viacep.com.br/ws/{cep}/json/");
            }
            catch
            {
                return null;
            }
        }
    }
}