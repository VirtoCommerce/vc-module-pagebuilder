import { Routes } from '@angular/router';
import { SidebarComponent } from './layout/sidebar/sidebar.component';

export const APP_ROUTES: Routes = [
    {
        path: '',
        component: SidebarComponent,
        children: [
            {
                path: 'pages',
                loadChildren: () => import('./modules/editor/editor.routes').then(r => r.EDITOR_ROUTES)
            },
            {
                path: 'themes',
                loadChildren: () => import('./modules/theme/theme.routes').then(r => r.THEME_ROUTES)
            },
            {
                path: '**',
                redirectTo: '/pages'
            }
        ]
    },
    {
        path: '**',
        redirectTo: '/pages'
    }
];
