using MesWorklog.Data;
using MesWorklog.Middleware;
using MesWorklog.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// swagger/openapi 서비스 등록
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// ProblemDetails(RFC 7807)를 기본 오류 응답 형식으로 사용
builder.Services.AddProblemDetails();
// Exception 핸들러 등록(GlobalExceptionHandler)
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
// Serviced
builder.Services.AddScoped<LineService>();
builder.Services.AddScoped<ProcessService>();
builder.Services.AddScoped<WorkerService>();
builder.Services.AddScoped<EquipmentService>();

var connectionString = builder.Configuration.GetConnectionString("MesDb")!;
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
           .UseSnakeCaseNamingConvention());

var app = builder.Build();

// 미들웨어는 맨 앞에둬야 먼저 실행됨
app.UseExceptionHandler();

// Configure the HTTP request pipeline.
// 개발환경에서만 swagger ui 노출
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "MES Worklog API"));
}

// Vite 프록시(localhost:5173 → 5201)는 http로만 통신하므로,
// https 프로필로 켜져 있어도 개발 환경에서는 https 강제 리다이렉트를 건너뛴다.
// (리다이렉트가 발생하면 브라우저가 7268로 직접 요청을 다시 보내면서
//  5173과 다른 출처가 되어 CORS 차단이 발생함)
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
