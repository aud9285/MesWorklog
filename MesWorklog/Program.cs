using MesWorklog.Data;
using MesWorklog.Middleware;
using MesWorklog.Services;
using Microsoft.EntityFrameworkCore;
using System.Data;       
using MySqlConnector;     

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
// Services
builder.Services.AddScoped<LineService>();
builder.Services.AddScoped<ProcessService>();
builder.Services.AddScoped<WorkerService>();
builder.Services.AddScoped<EquipmentService>();
builder.Services.AddScoped<WorkLogService>();
builder.Services.AddScoped<WorkOrderService>();
builder.Services.AddScoped<PauseReasonService>();
builder.Services.AddScoped<EffectivenessService>();



var connectionString = builder.Configuration.GetConnectionString("MesDb")!;
// AppDbContext - EFCore DbContext 하위 클래스
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
           .UseSnakeCaseNamingConvention());
// MySqlConnection - dapper raw sql 실행 
builder.Services.AddScoped<IDbConnection>(_ => new MySqlConnection(connectionString));

var app = builder.Build();

// 컨테이너는 EF 마이그레이션을 자동으로 돌리지 않는다.
// 배포 환경의 빈 DB에 스키마를 맞춰주기 위해 시작 시 한 번 실행한다.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

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

// Vue 빌드 결과(wwwroot)를 서빙한다.
// UseDefaultFiles가 "/" 요청을 index.html로 바꿔주고, UseStaticFiles가 실제 파일을 내보낸다.
// 순서가 중요 — UseDefaultFiles가 반드시 UseStaticFiles보다 먼저
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();

// 컨트롤러가 처리하지 못한 요청(= Vue 라우팅 경로)을 index.html로 보낸다.
// 반드시 MapControllers 뒤에 와야 한다 — 앞에 두면 /api 요청까지 삼켜버린다
app.MapFallbackToFile("index.html");

app.Run();
