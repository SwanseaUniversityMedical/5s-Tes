{{/*
Expand the name of the chart.
*/}}
{{- define "submission-stack.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
Part of the standard scaffold; stack templates do not call it.
*/}}
{{- define "submission-stack.fullname" -}}
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
{{- define "submission-stack.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels.
*/}}
{{- define "submission-stack.labels" -}}
helm.sh/chart: {{ include "submission-stack.chart" . }}
{{ include "submission-stack.selectorLabels" . }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/*
Selector labels.
*/}}
{{- define "submission-stack.selectorLabels" -}}
app.kubernetes.io/name: {{ include "submission-stack.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}
