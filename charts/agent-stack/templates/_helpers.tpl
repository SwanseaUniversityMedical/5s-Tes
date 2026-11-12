{{/*
Expand the name of the chart.
*/}}
{{- define "agent-stack.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
Part of the standard scaffold; stack templates do not call it.
*/}}
{{- define "agent-stack.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{/*
Chart name and version, used by the chart label.
*/}}
{{- define "agent-stack.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels.
*/}}
{{- define "agent-stack.labels" -}}
helm.sh/chart: {{ include "agent-stack.chart" . }}
{{ include "agent-stack.selectorLabels" . }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/*
Selector labels.
*/}}
{{- define "agent-stack.selectorLabels" -}}
app.kubernetes.io/name: {{ include "agent-stack.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Namespace the child ArgoCD Applications are created in.
*/}}
{{- define "agent-stack.argoNamespace" -}}
{{- .Values.global.argoNamespace | default .Values.global.namespace }}
{{- end }}

{{/*
Ingress hosts of RustFS and Seq. Both stacks use these prefixes, so on a
cluster running both under one global.ingress.host they need overriding.
*/}}
{{- define "agent-stack.rustfsHost" -}}
{{- .Values.rustfs.ingressHost | default (printf "rustfs.%s" .Values.global.ingress.host) }}
{{- end }}

{{- define "agent-stack.seqHost" -}}
{{- .Values.seq.ingressHost | default (printf "seq.%s" .Values.global.ingress.host) }}
{{- end }}
