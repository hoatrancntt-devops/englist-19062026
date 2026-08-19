using EnglishForIT.Infrastructure;
using EnglishForIT.Worker;
using Serilog;
using Serilog.Formatting.Compact;

var builder = Host.CreateApplicationBuilder(args);

// Cùng định dạng log với API: Docker gom stdout nên hai dịch vụ phải đọc được như nhau.
//
// Không đọc mức log từ file cấu hình như API: worker chỉ có một mức duy nhất và
// thêm gói Serilog.Settings.Configuration chỉ để làm việc đó là thừa.
builder.Services.AddSerilog(config => config
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console(new CompactJsonFormatter()));

// Dùng chung đăng ký với API: worker đọc ghi đúng những bảng đó, không có tầng riêng.
//
// Worker KHÔNG chạy migration — API làm việc đó. Hai nơi cùng migrate sẽ đâm nhau
// lúc nâng cấp, và thứ tự khởi động giữa hai container thì không ai đảm bảo được.
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
