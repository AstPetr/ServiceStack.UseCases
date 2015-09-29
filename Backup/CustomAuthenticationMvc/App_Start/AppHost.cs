﻿using System.Collections.Generic;
using System.Web.Security;
using Funq;
using ServiceStack;
using ServiceStack.Auth;
using ServiceStack.Caching;
using ServiceStack.Configuration;

namespace CustomAuthenticationMvc.App_Start
{
    public class AppHost : AppHostBase
    {
        public AppHost() : base("Custom Authentication Example", typeof(AppHost).Assembly) { }

        public override void Configure(Container container)
        {
            // register storage for user sessions 
            container.Register<ICacheClient>(new MemoryCacheClient());
            container.Register<ISessionFactory>(c => new SessionFactory(c.Resolve<ICacheClient>()));
            
            // uncomment for Redis 
            //container.Register<IRedisClientsManager>(c => new PooledRedisClientManager("localhost:6379"));
            //container.Register<ICacheClient>(c => (ICacheClient)c.Resolve<IRedisClientsManager>().GetCacheClient());

            // Register AuthFeature with custom user session and custom auth provider
            Plugins.Add(new AuthFeature(
                () => new CustomUserSession(), 
                new[] { new CustomCredentialsAuthProvider() }
            ));
        }
    }

    public class CustomUserSession : AuthUserSession
    {
        public string CompanyName { get; set; }
    }

    public class CustomCredentialsAuthProvider : CredentialsAuthProvider
    {
        public override bool TryAuthenticate(IServiceBase authService, string userName, string password)
        {
            if (!Membership.ValidateUser(userName, password)) return false;

            var session = (CustomUserSession)authService.GetSession(false);
            session.CompanyName = "Company from DB";
            session.UserAuthId = userName;
            session.IsAuthenticated = true;

            // add roles 
            session.Roles = new List<string>();
            if (session.UserAuthId == "admin") session.Roles.Add(RoleNames.Admin);
            session.Roles.Add("User");

            return true;
        }
    }

    [Authenticate]
    [Route("/hello")]
    public class HelloRequest : IReturn<HelloResponse>
    {
        public string Name { get; set; }
    }

    public class HelloResponse
    {
        public string Result { get; set; }
    }

    public class HelloService : Service
    {
        public const string HelloServiceCounterKey = "HelloServiceCounter";

        public object Any(HelloRequest request)
        {
            SessionBag.Set(HelloServiceCounterKey, SessionBag.Get<int>(HelloServiceCounterKey) + 1);
            var userSession = SessionAs<CustomUserSession>();
            var roles = string.Join(", ", userSession.Roles.ToArray());
            return new HelloResponse { Result = "Hello, " + request.Name + ", your role(s): " + roles};
        }
    }

}