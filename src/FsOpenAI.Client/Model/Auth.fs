namespace FsOpenAI.Client
open System
open Elmish
open FSharp.Control
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.WebAssembly.Authentication
open System.Security.Claims
open FsOpenAI.Shared

//manage user authentication
module Auth = 
    let getEmail (u:ClaimsPrincipal) = 
        u.Claims 
        |> Seq.tryFind(fun x -> x.Type="preferred_username") 
        |> Option.map(_.Value) 
        |> Option.defaultValue u.Identity.Name

    let initiateLogin() =
        async{
            do! Async.Sleep 1000    //post login after delay so user can see flash message
            return LoginLogout
        }

    let checkAuth model apply  =
        match model.appConfig.RequireLogin, model.user with
        | true,Unauthenticated                         -> model, Cmd.batch [Cmd.ofMsg (ShowInfo "Authenticating..."); (Cmd.OfAsync.perform initiateLogin () id) ]
        | true,Authenticated u when not u.IsAuthorized -> model, Cmd.ofMsg (ShowInfo "User not authorized")
        | _,_                                          -> apply model

    let checkAuthFlip apply model  = checkAuth model apply 

    let isAuthorized model =        
        match model.appConfig.RequireLogin, model.user with
        | true,Unauthenticated                         -> false
        | true,Authenticated u when not u.IsAuthorized -> false
        | _,_                                          -> true

    //Ultimately takes the user to the login/logout page of AD 
    let loginLogout (navMgr:NavigationManager) model =
        match model.user with 
        | Unauthenticated -> navMgr.NavigateToLogin("authentication/login")
        | Authenticated _  -> navMgr.NavigateToLogout("authentication/logout")           

    ///Post authentication processing
    let postAuth model (claimsPrincipal:ClaimsPrincipal option) =
        match claimsPrincipal with
        | None                                        -> {model with user=Unauthenticated}, Cmd.none
        | Some p when  not p.Identity.IsAuthenticated -> {model with user=Unauthenticated}, Cmd.none
        | Some p ->      
            //printfn $"Claims: %O{p.Identity.Name}"
            //p.Identities |> Seq.iter(fun x -> printfn $"{x.Name}")
            //p.Claims |> Seq.iter(fun x -> printfn $"{x.Type}={x.Value}")
            let claims = 
                p.Claims 
                |> Seq.tryFind(fun x ->x.Type="roles")
                |> Option.map(fun x->Text.Json.JsonSerializer.Deserialize<string list>(x.Value))
                |> Option.defaultValue []
                |> set
            let roles = model.appConfig.Roles |> set
            let userRoles = Set.intersect claims roles
            let hasAuth = model.appConfig.Roles.IsEmpty || not userRoles.IsEmpty
            let email = getEmail p
            let user = {Name=p.Identity.Name; IsAuthorized=hasAuth; Principal=p; Roles=userRoles; Email=email}            
            let model = 
                {model with 
                    user = UserState.Authenticated user
                    busy = hasAuth && Model.isChatPeristenceConfigured model
                }
            let cmds = 
                Cmd.batch 
                    [
                        if not hasAuth then 
                            Cmd.ofMsg (ShowError "User not authorized")
                        else 
                            Cmd.ofMsg StartInit
                        Cmd.ofMsg GetUserDetails
                    ]
            model,cmds
