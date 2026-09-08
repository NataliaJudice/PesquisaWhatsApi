using Microsoft.AspNetCore.Mvc;

namespace PesquisaWhatsApi.Controllers
{
    public class MensagemController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
