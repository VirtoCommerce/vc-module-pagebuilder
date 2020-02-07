cd ..\cms-designer
call npm ci
call npm run build
cd ..\store
call npm ci
call npm run build
cd ..\VirtoCommerce.PageBuilderModule.Web
call webpack --mode=production
