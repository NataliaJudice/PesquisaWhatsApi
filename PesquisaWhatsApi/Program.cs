using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using PesquisaWhatsApi.Appication.DTOs;
using PesquisaWhatsApi.Appication.Interface;
using PesquisaWhatsApi.Application.Services;
using PesquisaWhatsApi.Data;

var builder = WebApplication.CreateBuilder(args);

// 1. ADICIONA OS SERVIÇOS DO MVC (Mantenha apenas este para evitar conflitos)
builder.Services.AddControllersWithViews();

// Configura a conexão com o PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configuração da conta de WhatsApp (appsettings / user-secrets / variáveis de ambiente)
var secaoWhatsApp = builder.Configuration.GetSection(WhatsAppOptions.Secao);
builder.Services.Configure<WhatsAppOptions>(secaoWhatsApp);

// Injeção de Dependência dos Serviços
builder.Services.AddScoped<IChatbotService, ChatbotService>();
builder.Services.AddHttpClient();

// O provedor do WhatsApp é escolhido pela configuração: "Meta" (padrão) ou "Twilio"
var provedorWhatsApp = secaoWhatsApp.GetValue<string>("Provedor") ?? Provedores.Meta;

if (provedorWhatsApp.Equals(Provedores.Twilio, StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IWhatsAppService, TwilioWhatsAppService>();
}
else
{
    builder.Services.AddSingleton<IWhatsAppService, MetaWhatsAppService>();
}

// Quando a aplicação roda atrás de um túnel (ngrok) precisamos do host/esquema originais
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

// 2. CONFIGURAÇÕES DE MIDDLEWARE OBRIGATÓRIAS PARA MVC
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();

    // Em desenvolvimento o redirecionamento quebraria o POST do webhook vindo do túnel http
    app.UseHttpsRedirection();
}

app.UseStaticFiles(); // <-- OBRIGATÓRIO: Permite carregar o CSS/JS das Views

app.UseRouting();     // <-- OBRIGATÓRIO: Ativa o motor de rotas antes da autorização

app.UseAuthorization();

// 3. MAPEAMENTO DE ROTAS
app.MapControllers(); // Webhook do WhatsApp (api/whatsapp/...)

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Fluxo}/{action=Index}/{id?}");

// Executa automaticamente as migrações para criar as tabelas no Postgres
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

app.Run();
