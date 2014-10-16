﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.TestHost;
using Xunit;

namespace Microsoft.AspNet.Cache.Session
{
    public class SessionTests
    {
        [Fact]
        public async Task ReadingEmptySessionDoesNotCreateCookie()
        {
            using (var server = TestServer.Create(app =>
            {
                app.UseInMemorySession();
                app.Run(context =>
                {
                    Assert.Null(context.Session.GetString("NotFound"));
                    return Task.FromResult(0);
                });
            }))
            {
                var client = server.CreateClient();
                var response = await client.GetAsync("/");
                response.EnsureSuccessStatusCode();
                IEnumerable<string> values;
                Assert.False(response.Headers.TryGetValues("Set-Cookie", out values));
            }
        }

        [Fact]
        public async Task SettingAValueCausesTheCookieToBeCreated()
        {
            using (var server = TestServer.Create(app =>
            {
                app.UseInMemorySession();
                app.Run(context =>
                {
                    Assert.Null(context.Session.GetString("Key"));
                    context.Session.SetString("Key", "Value");
                    Assert.Equal("Value", context.Session.GetString("Key"));
                    return Task.FromResult(0);
                });
            }))
            {
                var client = server.CreateClient();
                var response = await client.GetAsync("/");
                response.EnsureSuccessStatusCode();
                IEnumerable<string> values;
                Assert.True(response.Headers.TryGetValues("Set-Cookie", out values));
                Assert.Equal(1, values.Count());
                Assert.True(!string.IsNullOrWhiteSpace(values.First()));
            }
        }

        [Fact]
        public async Task SessionCanBeAccessedOnTheNextRequest()
        {
            using (var server = TestServer.Create(app =>
            {
                app.UseInMemorySession();
                app.Run(context =>
                {
                    int? value = context.Session.GetInt("Key");
                    if (context.Request.Path == new PathString("/first"))
                    {
                        Assert.False(value.HasValue);
                        value = 0;
                    }
                    Assert.True(value.HasValue);
                    context.Session.SetInt("Key", value.Value + 1);
                    return context.Response.WriteAsync(value.Value.ToString());
                });
            }))
            {
                var client = server.CreateClient();
                var response = await client.GetAsync("/first");
                response.EnsureSuccessStatusCode();
                Assert.Equal("0", await response.Content.ReadAsStringAsync());

                client = server.CreateClient();
                client.DefaultRequestHeaders.Add("Cookie", response.Headers.GetValues("Set-Cookie"));
                Assert.Equal("1", await client.GetStringAsync("/"));
                Assert.Equal("2", await client.GetStringAsync("/"));
                Assert.Equal("3", await client.GetStringAsync("/"));
            }
        }

        [Fact]
        public async Task RemovedItemCannotBeAccessedAgain()
        {
            using (var server = TestServer.Create(app =>
            {
                app.UseInMemorySession();
                app.Run(context =>
                {
                    int? value = context.Session.GetInt("Key");
                    if (context.Request.Path == new PathString("/first"))
                    {
                        Assert.False(value.HasValue);
                        value = 0;
                        context.Session.SetInt("Key", 1);
                    }
                    else if (context.Request.Path == new PathString("/second"))
                    {
                        Assert.True(value.HasValue);
                        Assert.Equal(1, value);
                        context.Session.Remove("Key");
                    }
                    else if (context.Request.Path == new PathString("/third"))
                    {
                        Assert.False(value.HasValue);
                        value = 2;
                    }
                    return context.Response.WriteAsync(value.Value.ToString());
                });
            }))
            {
                var client = server.CreateClient();
                var response = await client.GetAsync("/first");
                response.EnsureSuccessStatusCode();
                Assert.Equal("0", await response.Content.ReadAsStringAsync());

                client = server.CreateClient();
                client.DefaultRequestHeaders.Add("Cookie", response.Headers.GetValues("Set-Cookie"));
                Assert.Equal("1", await client.GetStringAsync("/second"));
                Assert.Equal("2", await client.GetStringAsync("/third"));
            }
        }

        [Fact]
        public async Task ClearedItemsCannotBeAccessedAgain()
        {
            using (var server = TestServer.Create(app =>
            {
                app.UseInMemorySession();
                app.Run(context =>
                {
                    int? value = context.Session.GetInt("Key");
                    if (context.Request.Path == new PathString("/first"))
                    {
                        Assert.False(value.HasValue);
                        value = 0;
                        context.Session.SetInt("Key", 1);
                    }
                    else if (context.Request.Path == new PathString("/second"))
                    {
                        Assert.True(value.HasValue);
                        Assert.Equal(1, value);
                        context.Session.Clear();
                    }
                    else if (context.Request.Path == new PathString("/third"))
                    {
                        Assert.False(value.HasValue);
                        value = 2;
                    }
                    return context.Response.WriteAsync(value.Value.ToString());
                });
            }))
            {
                var client = server.CreateClient();
                var response = await client.GetAsync("/first");
                response.EnsureSuccessStatusCode();
                Assert.Equal("0", await response.Content.ReadAsStringAsync());

                client = server.CreateClient();
                client.DefaultRequestHeaders.Add("Cookie", response.Headers.GetValues("Set-Cookie"));
                Assert.Equal("1", await client.GetStringAsync("/second"));
                Assert.Equal("2", await client.GetStringAsync("/third"));
            }
        }
    }
}