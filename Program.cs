using Argus.Example.Data;
using Argus.Sync.Data.Models;
using Argus.Sync.Extensions;
using Microsoft.AspNetCore.Builder;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddCardanoIndexer<ArgusDbContext>(builder.Configuration);
builder.Services.AddReducers<ArgusDbContext, IReducerModel>(builder.Configuration);

WebApplication app = builder.Build();

app.Run();