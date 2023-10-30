//Call this to register our module to main application
var moduleTemplateName = "virtoCommerce.pageBuilderModule";

if (AppDependencies !== undefined) {
    AppDependencies.push(moduleTemplateName);
}

angular.module(moduleTemplateName, [])
    .config(
        ['virtoCommerce.contentModule.fileHandlerFactoryProvider',
            function (fileHandlerProvider) {
                fileHandlerProvider.addHandler('virtoCommerce.pageBuilderModule.fileHandler');
            }
        ]
    );
